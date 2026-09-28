using Zyven.Domain;
using Zyven.Application;
using Zyven.Infrastructure;
namespace UnitTests;

public class PaymentFoundationTests
{
    [Fact]
    public void Preparation_rejects_expired_inactive_or_invalid_snapshot()
    {
        var now = DateTimeOffset.UtcNow;
        var merchant = new MerchantAccount { OrganizationId = Guid.NewGuid(), Status = "ACTIVE" };
        var checkout = new CheckoutSession { OrganizationId = merchant.OrganizationId, CustomerId = Guid.NewGuid(), OfferId = Guid.NewGuid(), Price = 10m, Currency = "BRL", ExpiresAt = now };
        Assert.Throws<InvalidOperationException>(() => Payment.Prepare(checkout, merchant, 0m, now));
        checkout.ExpiresAt = now.AddMinutes(5); checkout.Status = "COMPLETED";
        Assert.Throws<InvalidOperationException>(() => Payment.Prepare(checkout, merchant, 0m, now));
        checkout.Status = "CREATED";
        foreach (var currency in new[] { "", "brl", "BRL\n", "US", "123" }) { checkout.Currency = currency; Assert.Throws<InvalidOperationException>(() => Payment.Prepare(checkout, merchant, 0m, now)); }
        checkout.Currency = "BRL"; checkout.CustomerId = Guid.Empty;
        Assert.Throws<InvalidOperationException>(() => Payment.Prepare(checkout, merchant, 0m, now));
    }
    [Fact]
    public void Amounts_preserve_exact_decimal_and_compute_net()
    {
        var amounts = new PaymentAmounts(123.45m, 3m, 5m, 1.23m, 0.80m);
        Assert.Equal(121.42m, amounts.NetAmount);
        Assert.Equal(123.45m, amounts.GrossAmount);
        Assert.Equal(0.80m, amounts.ProviderFee);
    }

    [Fact]
    public void Invalid_amounts_are_rejected_without_rounding()
    {
        foreach (var amount in new[] { -1m, 0.001m, 10000000000000000m })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PaymentAmounts(amount, 0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PaymentAmounts(10, amount, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PaymentAmounts(10, 0, amount, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PaymentAmounts(10, 0, 0, amount));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => new PaymentAmounts(0, 0, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PaymentAmounts(10, 0, 0, 11));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PaymentAmounts(10, 0, 0, 0, 11));
        Assert.Equal(0m, new PaymentAmounts(10, 0, 0, 10).NetAmount);
    }

    [Fact]
    public void Merchant_is_pending_and_payment_snapshot_is_authoritative()
    {
        var merchant = new MerchantAccount { OrganizationId = Guid.NewGuid() };
        Assert.Equal("PENDING", merchant.Status);
        var checkout = new CheckoutSession { OrganizationId = merchant.OrganizationId, CustomerId = Guid.NewGuid(), OfferId = Guid.NewGuid(), Price = 29.90m, Currency = "BRL", ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10) };
        Assert.Throws<InvalidOperationException>(() => Payment.Prepare(checkout, merchant, 0m, DateTimeOffset.UtcNow));
        merchant.Status = "ACTIVE";
        var payment = Payment.Prepare(checkout, merchant, 0m, DateTimeOffset.UtcNow);
        Assert.Equal(checkout.Price, payment.GrossAmount); Assert.Equal(checkout.Currency, payment.Currency); Assert.Equal(checkout.ExpiresAt, payment.ExpiresAt);
        Assert.Equal(checkout.CustomerId, payment.CustomerId); Assert.Equal("PENDING", payment.Status);
        var request = new PaymentChargeRequest(payment);
        Assert.Equal(payment.ExternalReference, request.IdempotencyReference);
        Assert.Equal(payment.ExpiresAt, request.ExpiresAt);
        Assert.Equal(payment.GrossAmount, request.Amounts.GrossAmount);
        Assert.Equal(payment.MerchantAccountId, request.MerchantAccountId);
        merchant.OrganizationId = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => Payment.Prepare(checkout, merchant, 0m, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Merchant_activation_requires_a_provider_recipient_identifier()
    {
        var merchant = new MerchantAccount { OrganizationId = Guid.NewGuid() };

        Assert.Throws<ArgumentException>(() => merchant.Activate(" ", DateTimeOffset.UtcNow));
        merchant.Activate("seller-123", DateTimeOffset.UtcNow);

        Assert.Equal("ACTIVE", merchant.Status);
        Assert.Equal("seller-123", merchant.ProviderRecipientId);
    }

    [Fact]
    public async Task Unconfigured_processor_returns_typed_unavailable_for_every_operation()
    {
        IPaymentProcessor processor = new UnconfiguredPaymentProcessor();
        Assert.False(processor.Capabilities.Pix); Assert.False(processor.Capabilities.Card); Assert.False(processor.Capabilities.Cancellation);
        Assert.Equal(PaymentOperationError.Unavailable, (await processor.CreatePixAsync(null!, default)).Error);
        Assert.Equal(PaymentOperationError.Unavailable, (await processor.CreateCardAsync(null!, default)).Error);
        Assert.Equal(PaymentOperationError.Unavailable, (await processor.QueryAsync(null!, default)).Error);
        Assert.Equal(PaymentOperationError.Unavailable, (await processor.CancelAsync(null!, default)).Error);
    }
}
