using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Zyven.Application;
using Zyven.Domain;
using Zyven.Infrastructure;

namespace UnitTests;

public sealed class SyncPayPaymentProcessorTests
{
    [Fact]
    public async Task Creates_pix_with_buyer_and_seller_split_and_reuses_token()
    {
        var handler = new RecordingHandler(
            Json(HttpStatusCode.OK, """{"access_token":"token-1","token_type":"Bearer","expires_in":3600}"""),
            Json(HttpStatusCode.OK, """{"message":"ok","pix_code":"000201-test","identifier":"tx-1"}"""),
            Json(HttpStatusCode.OK, """{"message":"ok","pix_code":"000201-next","identifier":"tx-2"}"""));
        var processor = new SyncPayPaymentProcessor(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.syncpayments.com.br") },
            Options.Create(new SyncPayOptions { ClientId = "client", ClientSecret = "secret", WebhookUrl = "https://api.zyven.test/webhooks/syncpay" }),
            TimeProvider.System);
        var request = ChargeRequest();

        var first = await processor.CreatePixAsync(request, default);
        var second = await processor.CreatePixAsync(request, default);

        Assert.Null(first.Error);
        Assert.Equal("tx-1", first.State!.ProviderTransactionId);
        Assert.Equal("000201-test", first.State.PixCode);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal("/api/partner/v1/auth-token", handler.Requests[0].Path);
        Assert.Equal("/api/partner/v1/cash-in", handler.Requests[1].Path);
        Assert.Equal("Bearer token-1", handler.Requests[1].Authorization);
        using var payload = JsonDocument.Parse(handler.Requests[1].Body);
        Assert.Equal(10m, payload.RootElement.GetProperty("amount").GetDecimal());
        Assert.Equal("Buyer", payload.RootElement.GetProperty("client").GetProperty("name").GetString());
        Assert.Equal("seller-user", payload.RootElement.GetProperty("split")[0].GetProperty("user_id").GetString());
        Assert.Equal(87m, payload.RootElement.GetProperty("split")[0].GetProperty("percentage").GetDecimal());
        Assert.Equal("https://api.zyven.test/webhooks/syncpay", payload.RootElement.GetProperty("webhook_url").GetString());
        Assert.Equal("tx-2", second.State!.ProviderTransactionId);
    }

    [Fact]
    public async Task Treats_timeout_as_indeterminate_and_validation_as_rejected()
    {
        var timeout = new RecordingHandler(Json(HttpStatusCode.OK, """{"access_token":"token","expires_in":3600}"""), new TimeoutException());
        var rejected = new RecordingHandler(Json(HttpStatusCode.OK, """{"access_token":"token","expires_in":3600}"""), Json(HttpStatusCode.UnprocessableEntity, "{}"));
        var options = Options.Create(new SyncPayOptions { ClientId = "client", ClientSecret = "secret", WebhookUrl = "https://api.zyven.test/webhooks/syncpay" });

        Assert.Equal(PaymentOperationError.Indeterminate, (await new SyncPayPaymentProcessor(new HttpClient(timeout) { BaseAddress = new("https://api.syncpayments.com.br") }, options, TimeProvider.System).CreatePixAsync(ChargeRequest(), default)).Error);
        Assert.Equal(PaymentOperationError.Rejected, (await new SyncPayPaymentProcessor(new HttpClient(rejected) { BaseAddress = new("https://api.syncpayments.com.br") }, options, TimeProvider.System).CreatePixAsync(ChargeRequest(), default)).Error);
    }

    private static PaymentChargeRequest ChargeRequest()
    {
        var now = DateTimeOffset.UtcNow;
        var merchant = new MerchantAccount { OrganizationId = Guid.NewGuid(), Status = "ACTIVE" };
        var checkout = new CheckoutSession { OrganizationId = merchant.OrganizationId, CustomerId = Guid.NewGuid(), OfferId = Guid.NewGuid(), Price = 10m, Currency = "BRL", ExpiresAt = now.AddMinutes(10) };
        var payment = Payment.Prepare(checkout, merchant, 0.50m, now, 0.80m);
        return new PaymentChargeRequest(payment, new PaymentPayer("Buyer", "12345678909", "buyer@example.test", "11999998888"), "seller-user");
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class RecordingHandler(params object[] responses) : HttpMessageHandler
    {
        private readonly Queue<object> _responses = new(responses);
        public List<(string Path, string Body, string? Authorization)> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.RequestUri!.AbsolutePath, request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken), request.Headers.Authorization?.ToString()));
            var response = _responses.Dequeue();
            if (response is Exception exception) throw exception;
            return (HttpResponseMessage)response;
        }
    }
}
