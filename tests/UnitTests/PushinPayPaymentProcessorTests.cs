using System.Net;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Zyven.Application;
using Zyven.Domain;
using Zyven.Infrastructure;

namespace UnitTests;

public sealed class PushinPayPaymentProcessorTests
{
    [Fact]
    public async Task Create_sends_integer_cents_webhook_and_platform_split()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, """{"id":"tx-1","qr_code":"000201","status":"created","value":1990,"qr_code_base64":"data:image/png;base64,AA=="}"""));
        var processor = Processor(handler);

        var result = await processor.CreatePixAsync(Request(19.90m, .50m), default);

        Assert.Null(result.Error);
        Assert.Equal("Bearer seller-token", handler.Authorization);
        Assert.Equal("/api/pix/cashIn", handler.Path);
        Assert.Equal(1990, handler.Json!.RootElement.GetProperty("value").GetInt32());
        Assert.Equal("https://api.example.test/api/webhooks/pushinpay/callback-secret", handler.Json.RootElement.GetProperty("webhook_url").GetString());
        Assert.Equal(50, handler.Json.RootElement.GetProperty("split_rules")[0].GetProperty("value").GetInt32());
        Assert.Equal("zyven-account", handler.Json.RootElement.GetProperty("split_rules")[0].GetProperty("account_id").GetString());
        Assert.Equal("tx-1", result.State!.ProviderTransactionId);
        Assert.Equal("000201", result.State.PixCode);
        Assert.Equal("data:image/png;base64,AA==", result.State.QrCodeData);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, PaymentOperationError.Rejected)]
    [InlineData(HttpStatusCode.UnprocessableEntity, PaymentOperationError.Rejected)]
    [InlineData(HttpStatusCode.TooManyRequests, PaymentOperationError.Indeterminate)]
    [InlineData(HttpStatusCode.ServiceUnavailable, PaymentOperationError.Indeterminate)]
    public async Task Create_maps_provider_failures(HttpStatusCode status, PaymentOperationError expected)
    {
        var processor = Processor(new RecordingHandler(_ => Json(status, "{}")));

        var result = await processor.CreatePixAsync(Request(19.90m, .50m), default);

        Assert.Equal(expected, result.Error);
    }

    [Theory]
    [InlineData("0.49", "0.10")]
    [InlineData("1.00", "0.51")]
    public async Task Create_rejects_invalid_amount_or_split(string gross, string fee)
    {
        var handler = new RecordingHandler(_ => throw new InvalidOperationException("Provider must not be called."));
        var result = await Processor(handler).CreatePixAsync(Request(decimal.Parse(gross, CultureInfo.InvariantCulture), decimal.Parse(fee, CultureInfo.InvariantCulture)), default);

        Assert.Equal(PaymentOperationError.Rejected, result.Error);
        Assert.Null(handler.Path);
    }

    [Fact]
    public async Task Query_maps_paid_transaction()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, """{"id":"tx/1","status":"paid","value":1990,"end_to_end_id":"E123","paid_at":"2026-10-07T12:00:00Z"}"""));
        var processor = Processor(handler);

        var result = await processor.QueryAsync(new PaymentLookup(Guid.NewGuid(), "PUSHINPAY", "reference", "tx/1", "seller-token"), default);

        Assert.Equal("/api/transaction/tx%2F1", handler.Path);
        Assert.Equal("PAID", result.State!.Status);
        Assert.Equal(19.90m, result.State.GrossAmount);
        Assert.Equal("E123", result.State.EndToEndId);
    }

    private static PushinPayPaymentProcessor Processor(HttpMessageHandler handler) => new(
        new HttpClient(handler) { BaseAddress = new("https://api-sandbox.pushinpay.com.br/api/") },
        Options.Create(new PushinPayOptions { Enabled = true, PlatformAccountId = "zyven-account", MaxSplitPercent = 50 }));

    private static PaymentChargeRequest Request(decimal gross, decimal fee)
    {
        var now = DateTimeOffset.UtcNow;
        var merchant = new MerchantAccount { OrganizationId = Guid.NewGuid(), Status = "ACTIVE" };
        var checkout = new CheckoutSession { OrganizationId = merchant.OrganizationId, CustomerId = Guid.NewGuid(), OfferId = Guid.NewGuid(), Price = gross, Currency = "BRL", ExpiresAt = now.AddMinutes(30) };
        var payment = Payment.Prepare(checkout, merchant, fee, now);
        return new(payment, new("Buyer", "12345678909", "buyer@example.test", ""), credential: new("seller-token", "https://api.example.test/api/webhooks/pushinpay/callback-secret"));
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string value) => new(status) { Content = new StringContent(value, Encoding.UTF8, "application/json") };

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public string? Authorization { get; private set; }
        public string? Path { get; private set; }
        public JsonDocument? Json { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            Path = request.RequestUri?.AbsolutePath;
            if (request.Content is not null) Json = JsonDocument.Parse(await request.Content.ReadAsStringAsync(cancellationToken));
            return response(request);
        }
    }
}
