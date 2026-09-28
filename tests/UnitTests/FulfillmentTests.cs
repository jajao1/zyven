using Zyven.Domain;
namespace UnitTests;

public class FulfillmentTests
{
    [Fact]
    public void Paid_payment_creates_active_entitlement_and_external_link_execution()
    {
        var now = DateTimeOffset.UtcNow; var org = Guid.NewGuid(); var offer = Guid.NewGuid(); var customer = Guid.NewGuid();
        var payment = Paid(org, offer, customer, now);
        var definition = FulfillmentDefinition.ExternalLink(org, offer, "Área do curso", "https://members.example.test/access", now);

        var entitlement = Entitlement.FromPaidPayment(payment, now);
        var execution = FulfillmentExecution.Deliver(entitlement, definition, now);

        Assert.Equal("ACTIVE", entitlement.Status); Assert.Equal(payment.Id, entitlement.PaymentId);
        Assert.Equal("COMPLETED", execution.Status); Assert.Equal("https://members.example.test/access", execution.ExternalUrl);
    }

    [Theory]
    [InlineData("http://example.test")]
    [InlineData("https://user:pass@example.test")]
    [InlineData("javascript:alert(1)")]
    public void External_link_requires_safe_https_url(string url) => Assert.Throws<ArgumentException>(() => FulfillmentDefinition.ExternalLink(Guid.NewGuid(), Guid.NewGuid(), "Access", url, DateTimeOffset.UtcNow));

    private static Payment Paid(Guid org, Guid offer, Guid customer, DateTimeOffset now)
    {
        var merchant = new MerchantAccount { OrganizationId = org, Status = "ACTIVE" };
        var checkout = new CheckoutSession { OrganizationId = org, CustomerId = customer, OfferId = offer, Price = 10, Currency = "BRL", ExpiresAt = now.AddMinutes(10) };
        var payment = Payment.Prepare(checkout, merchant, .50m, now); payment.BeginProvider(now); payment.AttachPix("CELCOIN", "tx", "identification", "emv", checkout.ExpiresAt, now); payment.ConfirmPaid("E123", 10, now); return payment;
    }
}
