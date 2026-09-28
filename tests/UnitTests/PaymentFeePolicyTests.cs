using Microsoft.Extensions.Options;
using Zyven.Infrastructure;

namespace UnitTests;

public sealed class PaymentFeePolicyTests
{
    [Fact]
    public void Uses_configured_fixed_fee_without_percentage_calculation()
    {
        var policy = new PaymentFeePolicy(Options.Create(new PaymentFeeOptions { PlatformFixedFee = 0.50m, ProviderFixedFee = 0.80m }));

        Assert.Equal(new PaymentFees(0.50m, 0.80m), policy.Calculate(10m));
        Assert.Equal(new PaymentFees(0.50m, 0.80m), policy.Calculate(500m));
    }

    [Fact]
    public void Rejects_an_invalid_configured_fee()
    {
        Assert.Throws<OptionsValidationException>(() =>
            new PaymentFeePolicy(Options.Create(new PaymentFeeOptions { PlatformFixedFee = -0.01m })));
        Assert.Throws<OptionsValidationException>(() =>
            new PaymentFeePolicy(Options.Create(new PaymentFeeOptions { PlatformFixedFee = 0.001m })));
    }

    [Fact]
    public void Rejects_a_sale_that_cannot_cover_the_fixed_fee()
    {
        var policy = new PaymentFeePolicy(Options.Create(new PaymentFeeOptions { PlatformFixedFee = 0.50m, ProviderFixedFee = 0.80m }));

        Assert.Throws<InvalidOperationException>(() => policy.Calculate(1.29m));
    }
}
