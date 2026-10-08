using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Zyven.Application;

namespace Zyven.Infrastructure;

public sealed class PushinPayPaymentProcessor(HttpClient http, IOptions<PushinPayOptions> configured) : IPaymentProcessor
{
    private const int MaximumResponseBytes = 256 * 1024;
    private static readonly JsonDocumentOptions JsonOptions = new() { MaxDepth = 24 };
    private readonly PushinPayOptions options = configured.Value;

    public PaymentCapabilities Capabilities => new(options.Enabled, false, false);

    public async Task<PaymentOperationResult> CreatePixAsync(PaymentChargeRequest request, CancellationToken ct)
    {
        if (!options.Enabled) return Error(PaymentOperationError.Unavailable);
        if (request.Currency != "BRL" || request.Credential is null || string.IsNullOrWhiteSpace(request.Credential.Token) ||
            !Uri.TryCreate(request.Credential.CallbackUrl, UriKind.Absolute, out var callback) || callback.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrWhiteSpace(options.PlatformAccountId) || !TryCents(request.Amounts.GrossAmount, out var grossCents) ||
            grossCents < 50 || !TryCents(request.Amounts.PlatformFee, out var feeCents) ||
            feeCents * 100L > grossCents * options.MaxSplitPercent)
            return Error(PaymentOperationError.Rejected);

        using var message = Authorized(HttpMethod.Post, "pix/cashIn", request.Credential.Token);
        message.Content = JsonContent.Create(new
        {
            value = grossCents,
            webhook_url = callback.AbsoluteUri,
            split_rules = new[] { new { value = feeCents, account_id = options.PlatformAccountId } }
        });

        try
        {
            using var response = await http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode) return Error(MapError(response.StatusCode));
            using var json = await ReadJson(response, ct);
            var root = json.RootElement;
            var id = String(root, "id");
            var pixCode = String(root, "qr_code");
            var responseValue = Int32(root, "value");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(pixCode) || responseValue != grossCents)
                return Error(PaymentOperationError.Indeterminate);

            return new(new(id, MapStatus(String(root, "status")), request.Amounts.GrossAmount, "BRL", null,
                request.ExpiresAt, String(root, "end_to_end_id"), pixCode, String(root, "qr_code_base64")), null);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return Error(PaymentOperationError.Indeterminate); }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidDataException or FormatException)
        {
            return Error(PaymentOperationError.Indeterminate);
        }
    }

    public async Task<PaymentOperationResult> QueryAsync(PaymentLookup request, CancellationToken ct)
    {
        if (!options.Enabled) return Error(PaymentOperationError.Unavailable);
        if (string.IsNullOrWhiteSpace(request.ProviderTransactionId) || string.IsNullOrWhiteSpace(request.Token))
            return Error(PaymentOperationError.Rejected);

        using var message = Authorized(HttpMethod.Get, $"transaction/{Uri.EscapeDataString(request.ProviderTransactionId)}", request.Token);
        try
        {
            using var response = await http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode) return Error(MapError(response.StatusCode));
            using var json = await ReadJson(response, ct);
            var root = json.RootElement;
            var id = String(root, "id");
            var value = Int32(root, "value");
            if (string.IsNullOrWhiteSpace(id) || id != request.ProviderTransactionId || value is null)
                return Error(PaymentOperationError.Indeterminate);

            return new(new(id, MapStatus(String(root, "status")), value.Value / 100m, "BRL",
                DateTime(root, "paid_at"), DateTime(root, "expires_at"), String(root, "end_to_end_id"),
                String(root, "qr_code"), String(root, "qr_code_base64")), null);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return Error(PaymentOperationError.Indeterminate); }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidDataException or FormatException)
        {
            return Error(PaymentOperationError.Indeterminate);
        }
    }

    public Task<PaymentOperationResult> CreateCardAsync(CardChargeRequest request, CancellationToken ct) => Task.FromResult(Error(PaymentOperationError.Unsupported));
    public Task<PaymentOperationResult> CancelAsync(PaymentLookup request, CancellationToken ct) => Task.FromResult(Error(PaymentOperationError.Unsupported));

    private static HttpRequestMessage Authorized(HttpMethod method, string path, string token)
    {
        var message = new HttpRequestMessage(method, path);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
        message.Headers.Accept.Add(new("application/json"));
        return message;
    }

    private static async Task<JsonDocument> ReadJson(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.Content.Headers.ContentLength > MaximumResponseBytes)
            throw new InvalidDataException("PushinPay response exceeds the allowed size.");
        await using var source = await response.Content.ReadAsStreamAsync(ct);
        await using var target = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var read = await source.ReadAsync(buffer, ct);
            if (read == 0) break;
            if (target.Length + read > MaximumResponseBytes) throw new InvalidDataException("PushinPay response exceeds the allowed size.");
            await target.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        target.Position = 0;
        return await JsonDocument.ParseAsync(target, JsonOptions, ct);
    }

    private static PaymentOperationError MapError(HttpStatusCode status) => status switch
    {
        HttpStatusCode.NotFound => PaymentOperationError.NotFound,
        HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests => PaymentOperationError.Indeterminate,
        _ when (int)status >= 500 => PaymentOperationError.Indeterminate,
        _ => PaymentOperationError.Rejected
    };

    private static string MapStatus(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        "paid" => "PAID",
        "canceled" or "cancelled" => "CANCELLED",
        "expired" => "EXPIRED",
        "created" or "pending" => "PENDING",
        _ => "PROCESSING"
    };

    private static string? String(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static int? Int32(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : null;
    private static DateTimeOffset? DateTime(JsonElement root, string name) => DateTimeOffset.TryParse(String(root, name), out var value) ? value : null;
    private static bool TryCents(decimal amount, out int cents)
    {
        cents = 0;
        if (amount < 0 || decimal.Truncate(amount * 100m) != amount * 100m || amount > int.MaxValue / 100m) return false;
        cents = decimal.ToInt32(amount * 100m);
        return true;
    }
    private static PaymentOperationResult Error(PaymentOperationError error) => new(null, error);
}
