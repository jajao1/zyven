using Zyven.Domain;
namespace UnitTests;

public class LedgerTests
{
    [Fact]
    public void Organization_chart_uses_provider_neutral_clearing()
    {
        var accounts = LedgerAccount.CreateChart(Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Contains(accounts, x => x.Code == "PAYMENT_PROCESSOR_CLEARING");
        Assert.DoesNotContain(accounts, x => x.Code == "CELCOIN_CLEARING");
    }

    [Theory]
    [InlineData("100.00", "2.00", "0.80")]
    [InlineData("19.90", "0.50", "0.00")]
    public void Captured_payment_creates_balanced_double_entries(string grossText, string platformText, string providerText)
    {
        var gross = decimal.Parse(grossText); var platform = decimal.Parse(platformText); var provider = decimal.Parse(providerText);
        var now = DateTimeOffset.UtcNow; var org = Guid.NewGuid();
        var merchant = new MerchantAccount { OrganizationId = org, Status = "ACTIVE" };
        var checkout = new CheckoutSession { OrganizationId = org, CustomerId = Guid.NewGuid(), OfferId = Guid.NewGuid(), Price = gross, Currency = "BRL", ExpiresAt = now.AddMinutes(10) };
        var payment = Payment.Prepare(checkout, merchant, platform, now, provider); payment.BeginProvider(now); payment.AttachPix("CELCOIN", "tx", "identification", "emv", checkout.ExpiresAt, now); payment.ConfirmPaid("E123", gross, now);
        var accounts = LedgerAccount.CreateChart(org, now).ToDictionary(x => x.Code);

        var transaction = LedgerTransaction.Capture(payment, accounts, now);

        Assert.Equal(gross, transaction.Entries.Sum(x => x.Debit)); Assert.Equal(gross, transaction.Entries.Sum(x => x.Credit));
        Assert.Equal(payment.NetAmount, transaction.Entries.Single(x => x.LedgerAccountId == accounts[LedgerAccountCodes.MerchantAvailable].Id).Credit);
        Assert.Equal(platform, transaction.Entries.Where(x => x.LedgerAccountId == accounts[LedgerAccountCodes.PlatformFeeRevenue].Id).Sum(x => x.Credit));
        Assert.Equal(provider, transaction.Entries.Where(x => x.LedgerAccountId == accounts[LedgerAccountCodes.ProviderFeePayable].Id).Sum(x => x.Credit));
    }

    [Fact]
    public void Capture_requires_a_paid_payment_and_matching_chart()
    {
        var now = DateTimeOffset.UtcNow; var org = Guid.NewGuid(); var merchant = new MerchantAccount { OrganizationId = org, Status = "ACTIVE" };
        var checkout = new CheckoutSession { OrganizationId = org, CustomerId = Guid.NewGuid(), OfferId = Guid.NewGuid(), Price = 10m, Currency = "BRL", ExpiresAt = now.AddMinutes(10) };
        var payment = Payment.Prepare(checkout, merchant, .50m, now);
        Assert.Throws<InvalidOperationException>(() => LedgerTransaction.Capture(payment, LedgerAccount.CreateChart(org, now).ToDictionary(x => x.Code), now));
        Assert.Throws<InvalidOperationException>(() => LedgerTransaction.Capture(payment, LedgerAccount.CreateChart(Guid.NewGuid(), now).ToDictionary(x => x.Code), now));
    }
}
