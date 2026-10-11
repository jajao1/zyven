using Zyven.Domain;
using Zyven.Infrastructure;
namespace UnitTests;

public sealed class TelegramTests
{
    private const string Key = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
    [Fact]
    public void Token_is_encrypted_with_random_nonce_and_round_trips()
    {
        var vault = new TelegramCredentialVault(Key); var first = vault.Encrypt("123:secret"); var second = vault.Encrypt("123:secret");
        Assert.NotEqual(first.Ciphertext, second.Ciphertext); Assert.NotEqual(first.Nonce, second.Nonce); Assert.Equal(first.Fingerprint, second.Fingerprint); Assert.Equal("123:secret", vault.Decrypt(first));
    }
    [Fact]
    public void Telegram_fulfillment_snapshots_integration_and_chat()
    {
        var now = DateTimeOffset.UtcNow; var org = Guid.NewGuid(); var offer = Guid.NewGuid(); var customer = Guid.NewGuid(); var merchant = new MerchantAccount { OrganizationId = org, Status = "ACTIVE" }; var checkout = new CheckoutSession { OrganizationId = org, CustomerId = customer, OfferId = offer, Price = 10, Currency = "BRL", ExpiresAt = now.AddMinutes(10) }; var payment = Payment.Prepare(checkout, merchant, .5m, now); payment.BeginProvider(now); payment.AttachPix("PUSHINPAY", "tx", "id", "emv", checkout.ExpiresAt, now); payment.ConfirmPaid("E123", 10, now); var entitlement = Entitlement.FromPaidPayment(payment, now);
        var integration = Guid.NewGuid(); var definition = FulfillmentDefinition.Telegram(payment.OrganizationId, payment.OfferId, integration, "-100123456", "Comunidade", now);
        var execution = FulfillmentExecution.Deliver(entitlement, definition, now);
        Assert.Equal("TELEGRAM_ACCESS", execution.Type); Assert.Equal(integration, execution.TelegramIntegrationId); Assert.Equal("-100123456", execution.TelegramChatId);
    }
    [Theory]
    [InlineData("")]
    [InlineData("public-link")]
    [InlineData("@")]
    public void Telegram_fulfillment_rejects_invalid_chat(string chat) => Assert.Throws<ArgumentException>(() => FulfillmentDefinition.Telegram(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), chat, "Grupo", DateTimeOffset.UtcNow));
}
