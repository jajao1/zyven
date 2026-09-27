using Microsoft.Extensions.Options;

namespace Zyven.Infrastructure;

public sealed class PaymentFeeOptions
{
    public const string SectionName = "Payments:Fees";
    public decimal PlatformFixedFee { get; set; } = 0.50m;
    public decimal ProviderFixedFee { get; set; } = 0.80m;
}

public sealed record PaymentFees(decimal PlatformFee, decimal ProviderFee);

public sealed class PaymentFeePolicy
{
    private readonly PaymentFees _fees;

    public PaymentFeePolicy(IOptions<PaymentFeeOptions> options)
    {
        _fees = new(options.Value.PlatformFixedFee, options.Value.ProviderFixedFee);
        if (!Valid(_fees.PlatformFee) || !Valid(_fees.ProviderFee))
            throw new OptionsValidationException(
                PaymentFeeOptions.SectionName,
                typeof(PaymentFeeOptions),
                ["Configured fees must be non-negative BRL amounts with at most two decimal places."]);
    }

    public PaymentFees Calculate(decimal grossAmount)
    {
        if (grossAmount <= 0 || decimal.Truncate(grossAmount * 100) != grossAmount * 100)
            throw new ArgumentOutOfRangeException(nameof(grossAmount));
        if (_fees.PlatformFee + _fees.ProviderFee > grossAmount)
            throw new InvalidOperationException("The sale amount cannot cover the configured fees.");
        return _fees;
    }

    private static bool Valid(decimal value) => value >= 0 && decimal.Truncate(value * 100) == value * 100;
}
