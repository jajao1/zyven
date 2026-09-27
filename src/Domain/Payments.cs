namespace Zyven.Domain;

public sealed record PaymentAmounts
{
    public decimal GrossAmount { get; }
    public decimal DiscountAmount { get; }
    public decimal OrderBumpAmount { get; }
    public decimal PlatformFee { get; }
    public decimal NetAmount => GrossAmount - PlatformFee;
    private static void Check(decimal value, string name) { if (!PaymentMoney.IsValid(value)) throw new ArgumentOutOfRangeException(name); }
    public PaymentAmounts(decimal grossAmount, decimal discountAmount, decimal orderBumpAmount, decimal platformFee)
    {
        Check(grossAmount, nameof(grossAmount)); Check(discountAmount, nameof(discountAmount)); Check(orderBumpAmount, nameof(orderBumpAmount)); Check(platformFee, nameof(platformFee));
        if (grossAmount == 0 || platformFee > grossAmount) throw new ArgumentOutOfRangeException(nameof(grossAmount));
        GrossAmount = grossAmount; DiscountAmount = discountAmount; OrderBumpAmount = orderBumpAmount; PlatformFee = platformFee;
    }
}
internal static class PaymentMoney
{
    internal static bool IsValid(decimal value) => value >= 0 && value <= 9999999999999999.99m && decimal.Truncate(value * 100) == value * 100;
}
public sealed class MerchantAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public string Status { get; set; } = "PENDING";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
public sealed class Payment
{
    private Payment() { }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OrganizationId { get; private set; }
    public Guid MerchantAccountId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid CheckoutSessionId { get; private set; }
    public Guid OfferId { get; private set; }
    public decimal GrossAmount { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal OrderBumpAmount { get; private set; }
    public decimal PlatformFee { get; private set; }
    public decimal NetAmount { get; private set; }
    public string Currency { get; private set; } = "";
    public string PaymentMethod { get; private set; } = "PIX";
    public string Status { get; private set; } = "PENDING";
    // Provider plus merchant scopes transaction IDs; references are never globally unique by assumption.
    public string? Provider { get; private set; }
    public string? ProviderTransactionId { get; private set; }
    public string ExternalReference { get; private set; } = Guid.NewGuid().ToString("N");
    public string? EndToEndId { get; private set; }
    public string? PixCode { get; private set; }
    public string? QrCodeData { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? PaidAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public static Payment Prepare(CheckoutSession checkout, MerchantAccount merchant, decimal platformFee, DateTimeOffset now)
    {
        if (merchant.Status != "ACTIVE" || merchant.OrganizationId != checkout.OrganizationId) throw new InvalidOperationException("An active merchant in the checkout organization is required.");
        if (checkout.Id == Guid.Empty || checkout.OrganizationId == Guid.Empty || checkout.CustomerId == Guid.Empty || checkout.OfferId == Guid.Empty || merchant.Id == Guid.Empty || checkout.Status != "CREATED" || checkout.ExpiresAt <= now || checkout.Currency is not { Length: 3 } || checkout.Currency.Any(c => c is < 'A' or > 'Z')) throw new InvalidOperationException("A valid, unexpired checkout snapshot is required.");
        var amounts = new PaymentAmounts(checkout.Price, 0m, 0m, platformFee);
        return new Payment
        {
            OrganizationId = checkout.OrganizationId,
            MerchantAccountId = merchant.Id,
            CustomerId = checkout.CustomerId,
            CheckoutSessionId = checkout.Id,
            OfferId = checkout.OfferId,
            GrossAmount = amounts.GrossAmount,
            DiscountAmount = amounts.DiscountAmount,
            OrderBumpAmount = amounts.OrderBumpAmount,
            PlatformFee = amounts.PlatformFee,
            NetAmount = amounts.NetAmount,
            Currency = checkout.Currency,
            ExpiresAt = checkout.ExpiresAt,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
