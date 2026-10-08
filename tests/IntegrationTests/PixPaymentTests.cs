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
using Npgsql;
using Zyven.Infrastructure;
using Zyven.Application;
namespace IntegrationTests;

public class PixPaymentTests
{
    private sealed class FakeProcessor : IPaymentProcessor, IPushinPayAccountValidator
    {
        public int Calls { get; private set; }
        public int QueryCalls { get; private set; }
        public PaymentCapabilities Capabilities => new(true, false, false);
        public Task<PaymentOperationResult> CreatePixAsync(PaymentChargeRequest request, CancellationToken ct) { Calls++; return Task.FromResult(new PaymentOperationResult(new("tx-1", "PENDING", request.Amounts.GrossAmount, "BRL", null, request.ExpiresAt, null, "000201-pix", "qr-image"), null)); }
        public Task<PaymentOperationResult> CreateCardAsync(CardChargeRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<PaymentOperationResult> QueryAsync(PaymentLookup request, CancellationToken ct) { QueryCalls++; return Task.FromResult(new PaymentOperationResult(new(request.ProviderTransactionId!, "PAID", 19.90m, "BRL", DateTimeOffset.Parse("2026-09-27T20:15:00Z"), null, "E123", null, null), null)); }
        public Task<PaymentOperationResult> CancelAsync(PaymentLookup request, CancellationToken ct) => throw new NotSupportedException();
        public Task<PaymentOperationError?> ValidateAsync(string token, CancellationToken ct) => Task.FromResult<PaymentOperationError?>(null);
    }

    [Fact]
    public async Task Checkout_creates_one_pix_charge_and_returns_it_idempotently()
    {
        var provider = new FakeProcessor();
        await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services => { services.RemoveAll<IPaymentProcessor>(); services.RemoveAll<IPushinPayAccountValidator>(); services.AddSingleton<IPaymentProcessor>(provider); services.AddSingleton<IPushinPayAccountValidator>(provider); }));
        var fixture = await PublicCheckoutTests.Fixture(app); using var owner = fixture.Client;
        (await owner.PutAsJsonAsync($"/api/organizations/{fixture.Org}/payment-account", new { token = "seller-token" })).EnsureSuccessStatusCode();
        using var buyer = app.CreateClient(new() { HandleCookies = false }); buyer.DefaultRequestHeaders.Add("X-Zyven-Client", "web");
        var checkoutResponse = await buyer.PostAsJsonAsync($"/api/public/offers/{fixture.Slug}/checkouts", new { name = "Buyer Name", email = "buyer@example.test", document = "12345678909", fields = new { } }); checkoutResponse.EnsureSuccessStatusCode();
        var checkout = await checkoutResponse.Content.ReadFromJsonAsync<CheckoutResponse>(); var cookie = checkoutResponse.Headers.GetValues("Set-Cookie").Single().Split(';')[0]; buyer.DefaultRequestHeaders.Add("Cookie", cookie);

        var first = await buyer.PostAsync($"/api/public/checkouts/{checkout!.Id}/payments/pix", null);
        var second = await buyer.PostAsync($"/api/public/checkouts/{checkout.Id}/payments/pix", null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode); Assert.Equal(HttpStatusCode.OK, second.StatusCode); Assert.Equal(1, provider.Calls);
        var payment = await first.Content.ReadFromJsonAsync<PixPaymentResponse>(); Assert.Equal("000201-pix", payment!.PixCode); Assert.Equal("PENDING", payment.Status); Assert.Equal("19.90", payment.Amount);
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ZyvenDbContext>();
        var stored = await db.Payments.SingleAsync(x => x.CheckoutSessionId == checkout.Id); Assert.Equal("PUSHINPAY", stored.Provider); Assert.Equal("tx-1", stored.ProviderTransactionId);
    }

    [Fact]
    public async Task PushinPay_webhook_verifies_provider_confirms_exact_amount_and_is_idempotent()
    {
        var provider = new FakeProcessor();
        await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services => { services.RemoveAll<IPaymentProcessor>(); services.RemoveAll<IPushinPayAccountValidator>(); services.AddSingleton<IPaymentProcessor>(provider); services.AddSingleton<IPushinPayAccountValidator>(provider); });
        });
        var fixture = await PublicCheckoutTests.Fixture(app); using var owner = fixture.Client;
        (await owner.PutAsJsonAsync($"/api/organizations/{fixture.Org}/offers/{fixture.Offer}/fulfillments/external-link", new { name = "Acessar curso", url = "https://members.example.test/course" })).EnsureSuccessStatusCode();
        (await owner.PutAsJsonAsync($"/api/organizations/{fixture.Org}/payment-account", new { token = "seller-token" })).EnsureSuccessStatusCode();
        using var buyer = app.CreateClient(new() { HandleCookies = false }); buyer.DefaultRequestHeaders.Add("X-Zyven-Client", "web");
        var checkoutResponse = await buyer.PostAsJsonAsync($"/api/public/offers/{fixture.Slug}/checkouts", new { name = "Buyer Name", email = "buyer2@example.test", document = "12345678909", fields = new { } });
        var checkout = await checkoutResponse.Content.ReadFromJsonAsync<CheckoutResponse>(); buyer.DefaultRequestHeaders.Add("Cookie", checkoutResponse.Headers.GetValues("Set-Cookie").Single().Split(';')[0]);
        (await buyer.PostAsync($"/api/public/checkouts/{checkout!.Id}/payments/pix", null)).EnsureSuccessStatusCode();
        using var anonymous = app.CreateClient(); Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/public/checkouts/{checkout.Id}/delivery")).StatusCode);
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ZyvenDbContext>(); var storedPayment = await db.Payments.SingleAsync(x => x.CheckoutSessionId == checkout.Id);
        var merchant = await db.MerchantAccounts.SingleAsync(x => x.Id == storedPayment.MerchantAccountId); var vault = scope.ServiceProvider.GetRequiredService<PushinPayCredentialVault>();
        var callbackSecret = vault.Decrypt(new(merchant.CallbackSecretCiphertext!, merchant.CallbackSecretNonce!, merchant.CallbackSecretTag!, ""));
        var webhookPath = $"/api/webhooks/pushinpay/{merchant.Id}/{callbackSecret}";
        using var webhook = app.CreateClient();
        var payload = new { id = "tx-1", value = 1990, status = "paid", end_to_end_id = "E123" };
        Assert.Equal(HttpStatusCode.OK, (await webhook.PostAsJsonAsync($"/api/webhooks/pushinpay/{merchant.Id}/{new string('A', 64)}", payload)).StatusCode);
        Assert.Equal("PENDING", (await buyer.GetFromJsonAsync<PixPaymentResponse>($"/api/public/checkouts/{checkout.Id}/payments/pix"))!.Status);
        Assert.Equal(HttpStatusCode.OK, (await webhook.PostAsJsonAsync(webhookPath, new { id = "tx-1", value = 1800, status = "paid", end_to_end_id = "E123" })).StatusCode);
        Assert.Equal("PENDING", (await buyer.GetFromJsonAsync<PixPaymentResponse>($"/api/public/checkouts/{checkout.Id}/payments/pix"))!.Status);
        Assert.Equal(HttpStatusCode.OK, (await webhook.PostAsJsonAsync(webhookPath, payload)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await webhook.PostAsJsonAsync(webhookPath, payload)).StatusCode);
        Assert.Equal(2, provider.QueryCalls);
        var paid = await buyer.GetFromJsonAsync<PixPaymentResponse>($"/api/public/checkouts/{checkout.Id}/payments/pix"); Assert.Equal("PAID", paid!.Status); Assert.Equal(DateTimeOffset.Parse("2026-09-27T20:15:00Z"), paid.PaidAt);
        var confirmations = await Task.WhenAll(
            webhook.PostAsJsonAsync(webhookPath, payload),
            webhook.PostAsJsonAsync(webhookPath, payload));
        Assert.All(confirmations, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.Equal(1, await db.PaymentWebhookEvents.CountAsync(x => x.PaymentId == paid.Id));
        db.ChangeTracker.Clear();
        var posting = await db.LedgerTransactions.Include(x => x.Entries).SingleAsync(x => x.PaymentId == paid.Id);
        Assert.Equal(19.90m, posting.Entries.Sum(x => x.Debit));
        Assert.Equal(19.90m, posting.Entries.Sum(x => x.Credit));
        Assert.Equal(1, await db.LedgerTransactions.CountAsync(x => x.PaymentId == paid.Id));
        var entitlementId = await db.Entitlements.Where(x => x.PaymentId == paid.Id).Select(x => x.Id).SingleAsync();
        Assert.Equal(1, await db.FulfillmentExecutions.CountAsync(x => x.EntitlementId == entitlementId));
        var delivery = await buyer.GetFromJsonAsync<DeliveryResponse>($"/api/public/checkouts/{checkout.Id}/delivery");
        Assert.Equal("ACTIVE", delivery!.Status); Assert.Single(delivery.Items); Assert.Equal("https://members.example.test/course", delivery.Items[0].Url);
        (await owner.PutAsJsonAsync($"/api/organizations/{fixture.Org}/offers/{fixture.Offer}/fulfillments/external-link", new { name = "Novo acesso", url = "https://members.example.test/new" })).EnsureSuccessStatusCode();
        var recovered = await buyer.GetFromJsonAsync<DeliveryResponse>($"/api/public/checkouts/{checkout.Id}/delivery");
        Assert.Equal("https://members.example.test/course", recovered!.Items.Single().Url);
        var wallet = await owner.GetFromJsonAsync<WalletResponse>($"/api/organizations/{fixture.Org}/finance/wallet");
        Assert.Equal("19.40", wallet!.AvailableBalance); Assert.Equal("19.90", wallet.TotalReceived); Assert.Equal("0.50", wallet.TotalFees);
        var history = await owner.GetFromJsonAsync<PageResponse<LedgerTransactionResponse>>($"/api/organizations/{fixture.Org}/finance/ledger");
        Assert.Single(history!.Items); Assert.Equal(3, history.Items[0].Entries.Count);
        var summary = await owner.GetFromJsonAsync<SalesSummaryResponse>($"/api/organizations/{fixture.Org}/finance/summary");
        Assert.Equal("19.40", summary!.AvailableBalance); Assert.Equal(1, summary.TotalPayments); Assert.Equal(1, summary.PaidPayments); Assert.Equal("19.40", summary.NetPaid);
        var sales = await owner.GetFromJsonAsync<PageResponse<SaleListItemResponse>>($"/api/organizations/{fixture.Org}/finance/sales?status=PAID");
        var sale = Assert.Single(sales!.Items); Assert.Equal("Buyer Name", sale.CustomerName); Assert.Equal("19.90", sale.GrossAmount); Assert.Equal("19.40", sale.NetAmount);
        var detail = await owner.GetFromJsonAsync<SaleDetailResponse>($"/api/organizations/{fixture.Org}/finance/sales/{paid.Id}");
        Assert.Equal("ACTIVE", detail!.EntitlementStatus); Assert.Equal("COMPLETED", detail.FulfillmentStatus); Assert.Equal("E123", detail.EndToEndId);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.GetAsync($"/api/organizations/{fixture.Org}/finance/sales?status=UNKNOWN")).StatusCode);
        var outsider = await PublicCheckoutTests.Fixture(app);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.Client.GetAsync($"/api/organizations/{fixture.Org}/finance/wallet")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.Client.GetAsync($"/api/organizations/{fixture.Org}/finance/sales/{paid.Id}")).StatusCode);
        var mutation = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"LedgerEntries\" SET \"Credit\" = 1 WHERE \"Id\" = {posting.Entries[0].Id}"));
        Assert.Equal(PostgresErrorCodes.ObjectNotInPrerequisiteState, mutation.SqlState);
        Assert.Equal(HttpStatusCode.BadRequest, (await webhook.PostAsync(webhookPath, new StringContent("{"))).StatusCode);
    }
}
