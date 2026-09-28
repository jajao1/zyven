using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Zyven.Application;
namespace Zyven.Infrastructure;

public sealed class CelcoinOptions
{
    public const string SectionName = "Payments:Celcoin";
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "https://sandbox.openfinance.celcoin.dev";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string PixKey { get; set; } = "";
    public string PlatformAccount { get; set; } = "";
    public int MaxSplitPercent { get; set; } = 10;
    public string CertificatePath { get; set; } = "";
    public string CertificatePassword { get; set; } = "";
    public string WebhookUsername { get; set; } = "";
    public string WebhookPassword { get; set; } = "";
}

public sealed class CelcoinPaymentProcessor(HttpClient http, IOptions<CelcoinOptions> configured, TimeProvider time) : IPaymentProcessor
{
    private readonly CelcoinOptions options = configured.Value;
    private readonly SemaphoreSlim tokenGate = new(1, 1);
    private string? token;
    private DateTimeOffset tokenExpiresAt;
    public PaymentCapabilities Capabilities => new(options.Enabled, false, false);

    public async Task<PaymentOperationResult> CreatePixAsync(PaymentChargeRequest request, CancellationToken ct)
    {
        if (!options.Enabled) return Error(PaymentOperationError.Unavailable);
        if (request.Currency != "BRL" || request.Payer is null || string.IsNullOrWhiteSpace(request.PixKey) || string.IsNullOrWhiteSpace(request.MerchantName) || string.IsNullOrWhiteSpace(request.MerchantCity) || string.IsNullOrWhiteSpace(request.MerchantPostalCode) || string.IsNullOrWhiteSpace(options.PlatformAccount)) return Error(PaymentOperationError.Rejected);
        var percent = (int)Math.Ceiling(request.Amounts.PlatformFee / request.Amounts.GrossAmount * 100m);
        if (percent > options.MaxSplitPercent) return Error(PaymentOperationError.Rejected);
        try
        {
            var bearer = await AccessToken(ct);
            var locationId = await CreateLocation(request, bearer, ct);
            if (locationId is null) return Error(PaymentOperationError.Rejected);
            return await CreateCharge(request, locationId.Value, percent, bearer, ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return Error(PaymentOperationError.Indeterminate); }
        catch (HttpRequestException) { return Error(PaymentOperationError.Indeterminate); }
        catch (JsonException) { return Error(PaymentOperationError.Indeterminate); }
    }

    private async Task<string> AccessToken(CancellationToken ct)
    {
        if (token is not null && tokenExpiresAt > time.GetUtcNow().AddMinutes(1)) return token;
        await tokenGate.WaitAsync(ct);
        try
        {
            if (token is not null && tokenExpiresAt > time.GetUtcNow().AddMinutes(1)) return token;
            using var form = new MultipartFormDataContent { { new StringContent(options.ClientId), "client_id" }, { new StringContent(options.ClientSecret), "client_secret" }, { new StringContent("client_credentials"), "grant_type" } };
            using var response = await http.PostAsync("/v5/token", form, ct); response.EnsureSuccessStatusCode();
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            token = json.RootElement.GetProperty("access_token").GetString() ?? throw new JsonException("Missing access token.");
            tokenExpiresAt = time.GetUtcNow().AddSeconds(json.RootElement.TryGetProperty("expires_in", out var expiry) ? expiry.GetInt32() : 300);
            return token;
        }
        finally { tokenGate.Release(); }
    }

    private async Task<long?> CreateLocation(PaymentChargeRequest request, string bearer, CancellationToken ct)
    {
        using var message = Authorized(HttpMethod.Post, "/pix/v1/location", bearer, new
        {
            clientRequestId = request.IdempotencyReference + "-location",
            type = "COB",
            merchant = new { postalCode = request.MerchantPostalCode, city = request.MerchantCity, merchantCategoryCode = "0000", name = request.MerchantName }
        });
        using var response = await http.SendAsync(message, ct);
        if (!response.IsSuccessStatusCode) { if ((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.TooManyRequests) throw new HttpRequestException(); return null; }
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return json.RootElement.GetProperty("locationId").GetInt64();
    }

    private async Task<PaymentOperationResult> CreateCharge(PaymentChargeRequest request, long locationId, int percent, string bearer, CancellationToken ct)
    {
        var document = new string(request.Payer!.Document.Where(char.IsAsciiDigit).ToArray());
        var expiration = Math.Max(60, (int)Math.Ceiling((request.ExpiresAt - time.GetUtcNow()).TotalSeconds));
        object debtor = document.Length == 14 ? new { name = request.Payer.Name, cnpj = document } : new { name = request.Payer.Name, cpf = document };
        using var message = Authorized(HttpMethod.Post, "/baas/v2/immediate/split", bearer, new
        {
            clientRequestId = request.IdempotencyReference,
            payerQuestion = "Pagamento Zyven",
            key = request.PixKey,
            locationId,
            debtor,
            amount = new { original = request.Amounts.GrossAmount, changeType = 0 },
            calendar = new { expiration },
            feeInfo = new { totalAmount = request.Amounts.PlatformFee, percent, feeDetails = new[] { new { amount = request.Amounts.PlatformFee, description = "Taxa Zyven", clientRequestId = request.IdempotencyReference + "-fee", accountCredit = options.PlatformAccount } } }
        });
        using var response = await http.SendAsync(message, ct);
        if (!response.IsSuccessStatusCode) return (int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.TooManyRequests ? Error(PaymentOperationError.Indeterminate) : Error(PaymentOperationError.Rejected);
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct); var root = json.RootElement;
        var transactionId = root.GetProperty("transactionId").ToString();
        var identification = root.TryGetProperty("transactionIdentification", out var tx) ? tx.GetString() : null;
        var emv = root.GetProperty("location").GetProperty("emv").GetString();
        if (string.IsNullOrWhiteSpace(transactionId) || string.IsNullOrWhiteSpace(emv)) return Error(PaymentOperationError.Indeterminate);
        return new(new(transactionId, "PENDING", request.Amounts.GrossAmount, request.Currency, null, request.ExpiresAt, null, emv, identification), null);
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string path, string bearer, object body)
    {
        var message = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) }; message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer); message.Headers.Accept.Add(new("application/json")); return message;
    }
    private static PaymentOperationResult Error(PaymentOperationError error) => new(null, error);
    public Task<PaymentOperationResult> CreateCardAsync(CardChargeRequest request, CancellationToken ct) => Task.FromResult(Error(PaymentOperationError.Unsupported));
    public Task<PaymentOperationResult> QueryAsync(PaymentLookup request, CancellationToken ct) => Task.FromResult(Error(PaymentOperationError.Unsupported));
    public Task<PaymentOperationResult> CancelAsync(PaymentLookup request, CancellationToken ct) => Task.FromResult(Error(PaymentOperationError.Unsupported));
}
