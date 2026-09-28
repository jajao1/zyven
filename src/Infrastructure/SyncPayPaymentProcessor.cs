using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Zyven.Application;

namespace Zyven.Infrastructure;

public sealed class SyncPayOptions
{
    public const string SectionName = "Payments:SyncPay";
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "https://api.syncpayments.com.br";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string WebhookUrl { get; set; } = "";
}

public sealed class SyncPayPaymentProcessor : IPaymentProcessor
{
    private readonly HttpClient _http;
    private readonly SyncPayOptions _options;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _token;
    private DateTimeOffset _tokenExpiresAt;

    public SyncPayPaymentProcessor(HttpClient http, IOptions<SyncPayOptions> options, TimeProvider clock)
    {
        _http = http;
        _options = options.Value;
        _clock = clock;
        if (string.IsNullOrWhiteSpace(_options.ClientId) || string.IsNullOrWhiteSpace(_options.ClientSecret))
            throw new OptionsValidationException(SyncPayOptions.SectionName, typeof(SyncPayOptions), ["ClientId and ClientSecret are required."]);
        if (!Uri.TryCreate(_options.WebhookUrl, UriKind.Absolute, out var webhook) || webhook.Scheme != Uri.UriSchemeHttps)
            throw new OptionsValidationException(SyncPayOptions.SectionName, typeof(SyncPayOptions), ["WebhookUrl must be an absolute HTTPS URL."]);
    }

    public PaymentCapabilities Capabilities => new(true, false, false);

    public async Task<PaymentOperationResult> CreatePixAsync(PaymentChargeRequest request, CancellationToken ct)
    {
        if (request.Payer is null || string.IsNullOrWhiteSpace(request.ProviderRecipientId) || request.Currency != "BRL")
            return new(null, PaymentOperationError.Rejected);

        try
        {
            var token = await Token(ct);
            using var message = new HttpRequestMessage(HttpMethod.Post, "/api/partner/v1/cash-in");
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var sellerPercentage = decimal.Round(request.Amounts.NetAmount / request.Amounts.GrossAmount * 100m, 6, MidpointRounding.ToEven);
            message.Content = JsonContent.Create(new CashIn(
                request.Amounts.GrossAmount,
                $"Zyven {request.IdempotencyReference}",
                _options.WebhookUrl,
                new Buyer(request.Payer.Name, request.Payer.Document, request.Payer.Email, request.Payer.Phone),
                [new Split(sellerPercentage, request.ProviderRecipientId)]));
            using var response = await _http.SendAsync(message, ct);
            if (response.StatusCode == HttpStatusCode.UnprocessableEntity) return new(null, PaymentOperationError.Rejected);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) return new(null, PaymentOperationError.Unavailable);
            if (!response.IsSuccessStatusCode) return new(null, (int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.TooManyRequests ? PaymentOperationError.Indeterminate : PaymentOperationError.Rejected);
            var result = await response.Content.ReadFromJsonAsync<CashInResponse>(cancellationToken: ct);
            if (result is null || string.IsNullOrWhiteSpace(result.Identifier) || string.IsNullOrWhiteSpace(result.PixCode)) return new(null, PaymentOperationError.Indeterminate);
            return new(new PaymentProviderState(result.Identifier, "PENDING", request.Amounts.GrossAmount, request.Currency, null, request.ExpiresAt, null, result.PixCode, null), null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            return new(null, PaymentOperationError.Indeterminate);
        }
    }

    public Task<PaymentOperationResult> CreateCardAsync(CardChargeRequest request, CancellationToken ct) => Task.FromResult(new PaymentOperationResult(null, PaymentOperationError.Unsupported));
    public Task<PaymentOperationResult> QueryAsync(PaymentLookup request, CancellationToken ct) => Task.FromResult(new PaymentOperationResult(null, PaymentOperationError.Unsupported));
    public Task<PaymentOperationResult> CancelAsync(PaymentLookup request, CancellationToken ct) => Task.FromResult(new PaymentOperationResult(null, PaymentOperationError.Unsupported));

    private async Task<string> Token(CancellationToken ct)
    {
        if (_token is not null && _tokenExpiresAt > _clock.GetUtcNow().AddMinutes(1)) return _token;
        await _tokenLock.WaitAsync(ct);
        try
        {
            if (_token is not null && _tokenExpiresAt > _clock.GetUtcNow().AddMinutes(1)) return _token;
            using var response = await _http.PostAsJsonAsync("/api/partner/v1/auth-token", new AuthRequest(_options.ClientId, _options.ClientSecret), ct);
            response.EnsureSuccessStatusCode();
            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: ct) ?? throw new HttpRequestException("SyncPay returned an empty authentication response.");
            if (string.IsNullOrWhiteSpace(auth.AccessToken) || auth.ExpiresIn <= 0) throw new HttpRequestException("SyncPay returned an invalid authentication response.");
            _token = auth.AccessToken;
            _tokenExpiresAt = _clock.GetUtcNow().AddSeconds(auth.ExpiresIn);
            return _token;
        }
        finally { _tokenLock.Release(); }
    }

    private sealed record AuthRequest([property: JsonPropertyName("client_id")] string ClientId, [property: JsonPropertyName("client_secret")] string ClientSecret);
    private sealed record AuthResponse([property: JsonPropertyName("access_token")] string AccessToken, [property: JsonPropertyName("expires_in")] int ExpiresIn);
    private sealed record Buyer(string Name, [property: JsonPropertyName("cpf")] string Document, string Email, string Phone);
    private sealed record Split(decimal Percentage, [property: JsonPropertyName("user_id")] string UserId);
    private sealed record CashIn(decimal Amount, string Description, [property: JsonPropertyName("webhook_url")] string WebhookUrl, Buyer Client, IReadOnlyList<Split> Split);
    private sealed record CashInResponse([property: JsonPropertyName("pix_code")] string PixCode, string Identifier);
}
