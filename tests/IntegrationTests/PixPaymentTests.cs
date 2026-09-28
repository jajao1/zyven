using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Zyven.Infrastructure;
using Zyven.Application;
namespace IntegrationTests;

public class PixPaymentTests
{
    private sealed class FakeProcessor : IPaymentProcessor
    {
        public int Calls { get; private set; }
        public PaymentCapabilities Capabilities => new(true, false, false);
        public Task<PaymentOperationResult> CreatePixAsync(PaymentChargeRequest request, CancellationToken ct) { Calls++; return Task.FromResult(new PaymentOperationResult(new("celcoin-1", "PENDING", request.Amounts.GrossAmount, "BRL", null, request.ExpiresAt, null, "000201-pix", "tx-identification"), null)); }
        public Task<PaymentOperationResult> CreateCardAsync(CardChargeRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<PaymentOperationResult> QueryAsync(PaymentLookup request, CancellationToken ct) => throw new NotSupportedException();
        public Task<PaymentOperationResult> CancelAsync(PaymentLookup request, CancellationToken ct) => throw new NotSupportedException();
    }

    [Fact]
    public async Task Checkout_creates_one_pix_charge_and_returns_it_idempotently()
    {
        var provider = new FakeProcessor();
        await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services => { services.RemoveAll<IPaymentProcessor>(); services.AddSingleton<IPaymentProcessor>(provider); }));
        var fixture = await PublicCheckoutTests.Fixture(app); using var owner = fixture.Client;
        (await owner.PutAsJsonAsync($"/api/organizations/{fixture.Org}/payment-account", new { providerRecipientId = "seller-" + Guid.NewGuid().ToString("N"), pixKey = "seller@example.test", merchantName = "PUBLIC STUDIO", merchantCity = "SAO PAULO", merchantPostalCode = "01001000" })).EnsureSuccessStatusCode();
        using var buyer = app.CreateClient(new() { HandleCookies = false }); buyer.DefaultRequestHeaders.Add("X-Zyven-Client", "web");
        var checkoutResponse = await buyer.PostAsJsonAsync($"/api/public/offers/{fixture.Slug}/checkouts", new { name = "Buyer Name", email = "buyer@example.test", document = "12345678909", fields = new { } }); checkoutResponse.EnsureSuccessStatusCode();
        var checkout = await checkoutResponse.Content.ReadFromJsonAsync<CheckoutResponse>(); var cookie = checkoutResponse.Headers.GetValues("Set-Cookie").Single().Split(';')[0]; buyer.DefaultRequestHeaders.Add("Cookie", cookie);

        var first = await buyer.PostAsync($"/api/public/checkouts/{checkout!.Id}/payments/pix", null);
        var second = await buyer.PostAsync($"/api/public/checkouts/{checkout.Id}/payments/pix", null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode); Assert.Equal(HttpStatusCode.OK, second.StatusCode); Assert.Equal(1, provider.Calls);
        var payment = await first.Content.ReadFromJsonAsync<PixPaymentResponse>(); Assert.Equal("000201-pix", payment!.PixCode); Assert.Equal("PENDING", payment.Status); Assert.Equal("19.90", payment.Amount);
    }

    [Fact]
    public async Task Celcoin_webhook_requires_auth_confirms_exact_amount_and_is_idempotent()
    {
        var provider = new FakeProcessor();
        await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["Payments:Celcoin:WebhookUsername"] = "celcoin", ["Payments:Celcoin:WebhookPassword"] = "secret" }));
            builder.ConfigureServices(services => { services.RemoveAll<IPaymentProcessor>(); services.AddSingleton<IPaymentProcessor>(provider); });
        });
        var fixture = await PublicCheckoutTests.Fixture(app); using var owner = fixture.Client;
        (await owner.PutAsJsonAsync($"/api/organizations/{fixture.Org}/payment-account", new { providerRecipientId = "seller-" + Guid.NewGuid().ToString("N"), pixKey = "seller@example.test", merchantName = "PUBLIC STUDIO", merchantCity = "SAO PAULO", merchantPostalCode = "01001000" })).EnsureSuccessStatusCode();
        using var buyer = app.CreateClient(new() { HandleCookies = false }); buyer.DefaultRequestHeaders.Add("X-Zyven-Client", "web");
        var checkoutResponse = await buyer.PostAsJsonAsync($"/api/public/offers/{fixture.Slug}/checkouts", new { name = "Buyer Name", email = "buyer2@example.test", document = "12345678909", fields = new { } });
        var checkout = await checkoutResponse.Content.ReadFromJsonAsync<CheckoutResponse>(); buyer.DefaultRequestHeaders.Add("Cookie", checkoutResponse.Headers.GetValues("Set-Cookie").Single().Split(';')[0]);
        (await buyer.PostAsync($"/api/public/checkouts/{checkout!.Id}/payments/pix", null)).EnsureSuccessStatusCode();
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ZyvenDbContext>(); var reference = await db.Payments.Where(x => x.CheckoutSessionId == checkout.Id).Select(x => x.ExternalReference).SingleAsync();
        using var webhook = app.CreateClient();
        var eventId = "event-" + Guid.NewGuid().ToString("N");
        var payload = new { webhookId = eventId, status = "CONFIRMED", createTimestamp = "2026-09-27T20:15:00Z", RequestBody = new { ClientRequestId = reference, TransactionIdBRCode = "celcoin-1", Amount = 19.90m, EndToEndId = "E123" } };
        Assert.Equal(HttpStatusCode.Unauthorized, (await webhook.PostAsJsonAsync("/api/webhooks/celcoin", payload)).StatusCode);
        webhook.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("celcoin:secret")));
        var mismatch = new { webhookId = eventId + "-mismatch", status = "CONFIRMED", RequestBody = new { ClientRequestId = reference, TransactionIdBRCode = "celcoin-1", Amount = 18m, EndToEndId = "E-wrong" } };
        Assert.Equal(HttpStatusCode.OK, (await webhook.PostAsJsonAsync("/api/webhooks/celcoin", mismatch)).StatusCode);
        Assert.Equal("PENDING", (await buyer.GetFromJsonAsync<PixPaymentResponse>($"/api/public/checkouts/{checkout.Id}/payments/pix"))!.Status);
        Assert.Equal(HttpStatusCode.OK, (await webhook.PostAsJsonAsync("/api/webhooks/celcoin", payload)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await webhook.PostAsJsonAsync("/api/webhooks/celcoin", payload)).StatusCode);
        var paid = await buyer.GetFromJsonAsync<PixPaymentResponse>($"/api/public/checkouts/{checkout.Id}/payments/pix"); Assert.Equal("PAID", paid!.Status); Assert.Equal(DateTimeOffset.Parse("2026-09-27T20:15:00Z"), paid.PaidAt);
        Assert.Equal(2, await db.PaymentWebhookEvents.CountAsync(x => x.PaymentId == paid.Id));
        var parallelId = "event-parallel-" + Guid.NewGuid().ToString("N"); var unknown = new { webhookId = parallelId, status = "CONFIRMED", RequestBody = new { ClientRequestId = "unknown", Amount = 19.90m, EndToEndId = "E-unknown" } };
        var parallel = await Task.WhenAll(webhook.PostAsJsonAsync("/api/webhooks/celcoin", unknown), webhook.PostAsJsonAsync("/api/webhooks/celcoin", unknown)); Assert.All(parallel, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        db.ChangeTracker.Clear(); Assert.Equal(1, await db.PaymentWebhookEvents.CountAsync(x => x.ExternalEventId == parallelId));
    }
}
