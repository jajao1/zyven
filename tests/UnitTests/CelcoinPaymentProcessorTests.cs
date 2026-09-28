using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Zyven.Application;
using Zyven.Domain;
using Zyven.Infrastructure;
namespace UnitTests;

public class CelcoinPaymentProcessorTests
{
    [Fact]
    public async Task Creates_location_then_fixed_split_with_stable_references()
    {
        var calls = new List<(string Path, string Body)>();
        var handler = new StubHandler(async request =>
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(); calls.Add((request.RequestUri!.AbsolutePath, body));
            return request.RequestUri.AbsolutePath switch
            {
                "/v5/token" => Json("{\"access_token\":\"token\",\"expires_in\":3600}"),
                "/pix/v1/location" => Json("{\"status\":\"CREATED\",\"locationId\":12730559,\"emv\":\"location-emv\"}"),
                _ => Json("{\"transactionId\":9163565,\"status\":\"ACTIVE\",\"transactionIdentification\":\"tx-identification\",\"location\":{\"emv\":\"pix-emv\"},\"calendar\":{\"expiration\":1800}}")
            };
        });
        var processor = new CelcoinPaymentProcessor(new HttpClient(handler) { BaseAddress = new("https://sandbox.openfinance.celcoin.dev") }, Options.Create(new CelcoinOptions { Enabled = true, ClientId = "id", ClientSecret = "secret", PixKey = "key", PlatformAccount = "platform" }), TimeProvider.System);
        var merchant = new MerchantAccount { OrganizationId = Guid.NewGuid(), Status = "ACTIVE" }; merchant.Activate("seller", DateTimeOffset.UtcNow, "seller-key", "SELLER", "BARUERI", "06455030");
        var checkout = new CheckoutSession { OrganizationId = merchant.OrganizationId, CustomerId = Guid.NewGuid(), OfferId = Guid.NewGuid(), Price = 19.90m, Currency = "BRL", ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30) };
        var payment = Payment.Prepare(checkout, merchant, .50m, DateTimeOffset.UtcNow);
        var request = new PaymentChargeRequest(payment, new("Buyer", "12345678909", "buyer@example.test", ""), merchant.ProviderRecipientId, merchant.PixKey, merchant.MerchantName, merchant.MerchantCity, merchant.MerchantPostalCode);

        var result = await processor.CreatePixAsync(request, default);

        Assert.Null(result.Error); Assert.Equal("9163565", result.State!.ProviderTransactionId); Assert.Equal("pix-emv", result.State.PixCode);
        Assert.Equal(["/v5/token", "/pix/v1/location", "/baas/v2/immediate/split"], calls.Select(x => x.Path));
        Assert.Contains(payment.ExternalReference + "-location", calls[1].Body); Assert.Contains("\"totalAmount\":0.50", calls[2].Body); Assert.Contains("\"accountCredit\":\"platform\"", calls[2].Body);
    }
    private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK) { Content = new StringContent(value, Encoding.UTF8, "application/json") };
    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> callback) : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => callback(request); }
}
