namespace Zyven.Domain;

public sealed record PaymentAmounts
{
    public decimal GrossAmount { get; }
    public decimal DiscountAmount { get; }
    public decimal OrderBumpAmount { get; }
    public decimal PlatformFee { get; }
    public decimal ProviderFee { get; }
    public decimal NetAmount => GrossAmount - PlatformFee - ProviderFee;
    private static void Check(decimal value, string name) { if (!PaymentMoney.IsValid(value)) throw new ArgumentOutOfRangeException(name); }
    public PaymentAmounts(decimal grossAmount, decimal discountAmount, decimal orderBumpAmount, decimal platformFee, decimal providerFee = 0m)
    {
        Check(grossAmount, nameof(grossAmount)); Check(discountAmount, nameof(discountAmount)); Check(orderBumpAmount, nameof(orderBumpAmount)); Check(platformFee, nameof(platformFee)); Check(providerFee, nameof(providerFee));
        if (grossAmount == 0 || platformFee + providerFee > grossAmount) throw new ArgumentOutOfRangeException(nameof(grossAmount));
        GrossAmount = grossAmount; DiscountAmount = discountAmount; OrderBumpAmount = orderBumpAmount; PlatformFee = platformFee; ProviderFee = providerFee;
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
    public string? ProviderRecipientId { get; private set; }
    public string? PixKey { get; private set; }
    public string? MerchantName { get; private set; }
    public string? MerchantCity { get; private set; }
    public string? MerchantPostalCode { get; private set; }
    public string? Provider { get; private set; }
    public string? CredentialCiphertext { get; private set; }
    public string? CredentialNonce { get; private set; }
    public string? CredentialTag { get; private set; }
    public string? CredentialFingerprint { get; private set; }
    public string? CallbackSecretCiphertext { get; private set; }
    public string? CallbackSecretNonce { get; private set; }
    public string? CallbackSecretTag { get; private set; }
    public string? CallbackSecretHash { get; private set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public void Activate(string providerRecipientId, DateTimeOffset now, string? pixKey = null, string? merchantName = null, string? merchantCity = null, string? merchantPostalCode = null)
    {
        if (string.IsNullOrWhiteSpace(providerRecipientId) || providerRecipientId.Length > 200) throw new ArgumentException("A valid provider recipient identifier is required.", nameof(providerRecipientId));
        if (pixKey is not null && (string.IsNullOrWhiteSpace(pixKey) || pixKey.Length > 200)) throw new ArgumentException("A valid PIX key is required.", nameof(pixKey));
        if (merchantName is not null && (string.IsNullOrWhiteSpace(merchantName) || merchantName.Length > 25)) throw new ArgumentException("A valid merchant name is required.", nameof(merchantName));
        if (merchantCity is not null && (string.IsNullOrWhiteSpace(merchantCity) || merchantCity.Length > 15)) throw new ArgumentException("A valid merchant city is required.", nameof(merchantCity));
        if (merchantPostalCode is not null && (merchantPostalCode.Length != 8 || merchantPostalCode.Any(c => !char.IsAsciiDigit(c)))) throw new ArgumentException("A valid merchant postal code is required.", nameof(merchantPostalCode));
        ProviderRecipientId = providerRecipientId.Trim(); PixKey = pixKey?.Trim(); MerchantName = merchantName?.Trim(); MerchantCity = merchantCity?.Trim(); MerchantPostalCode = merchantPostalCode;
        Status = "ACTIVE"; UpdatedAt = now;
    }

    public void ConnectPushinPay(
        string credentialCiphertext, string credentialNonce, string credentialTag, string credentialFingerprint,
        string callbackSecretCiphertext, string callbackSecretNonce, string callbackSecretTag, string callbackSecretHash,
        DateTimeOffset now)
    {
        var values = new[] { credentialCiphertext, credentialNonce, credentialTag, credentialFingerprint, callbackSecretCiphertext, callbackSecretNonce, callbackSecretTag, callbackSecretHash };
        if (values.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Complete encrypted PushinPay credentials are required.");
        if (credentialFingerprint.Length != 12 || callbackSecretHash.Length != 64) throw new ArgumentException("Invalid PushinPay credential metadata.");

        Provider = "PUSHINPAY";
        CredentialCiphertext = credentialCiphertext;
        CredentialNonce = credentialNonce;
        CredentialTag = credentialTag;
        CredentialFingerprint = credentialFingerprint;
        CallbackSecretCiphertext = callbackSecretCiphertext;
        CallbackSecretNonce = callbackSecretNonce;
        CallbackSecretTag = callbackSecretTag;
        CallbackSecretHash = callbackSecretHash;
        ProviderRecipientId = null;
        PixKey = null;
        MerchantName = null;
        MerchantCity = null;
        MerchantPostalCode = null;
        Status = "ACTIVE";
        UpdatedAt = now;
    }

    public void Disconnect(DateTimeOffset now)
    {
        Status = "PENDING";
        UpdatedAt = now;
    }
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
    public decimal ProviderFee { get; private set; }
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
    public static Payment Prepare(CheckoutSession checkout, MerchantAccount merchant, decimal platformFee, DateTimeOffset now, decimal providerFee = 0m)
    {
        if (merchant.Status != "ACTIVE" || merchant.OrganizationId != checkout.OrganizationId) throw new InvalidOperationException("An active merchant in the checkout organization is required.");
        if (checkout.Id == Guid.Empty || checkout.OrganizationId == Guid.Empty || checkout.CustomerId == Guid.Empty || checkout.OfferId == Guid.Empty || merchant.Id == Guid.Empty || checkout.Status != "CREATED" || checkout.ExpiresAt <= now || checkout.Currency is not { Length: 3 } || checkout.Currency.Any(c => c is < 'A' or > 'Z')) throw new InvalidOperationException("A valid, unexpired checkout snapshot is required.");
        var amounts = new PaymentAmounts(checkout.Price, 0m, 0m, platformFee, providerFee);
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
            ProviderFee = amounts.ProviderFee,
            NetAmount = amounts.NetAmount,
            Currency = checkout.Currency,
            ExpiresAt = checkout.ExpiresAt,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
    public void BeginProvider(DateTimeOffset now)
    {
        if (Status != "PENDING") throw new InvalidOperationException("Only a pending payment can start provider processing.");
        Status = "PROCESSING"; UpdatedAt = now;
    }
    public void AttachPix(string provider, string providerTransactionId, string? transactionIdentification, string pixCode, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        if (Status != "PROCESSING" || string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(providerTransactionId) || string.IsNullOrWhiteSpace(pixCode)) throw new InvalidOperationException("A processing payment and complete PIX response are required.");
        Provider = provider.Trim(); ProviderTransactionId = providerTransactionId.Trim(); QrCodeData = transactionIdentification; PixCode = pixCode; ExpiresAt = expiresAt; Status = "PENDING"; UpdatedAt = now;
    }
    public void Fail(DateTimeOffset now)
    {
        if (Status is not ("PENDING" or "PROCESSING")) throw new InvalidOperationException("This payment cannot fail from its current state.");
        Status = "FAILED"; UpdatedAt = now;
    }
    public void Expire(DateTimeOffset now)
    {
        if (Status is not ("PENDING" or "PROCESSING") || ExpiresAt > now) throw new InvalidOperationException("This payment is not eligible for expiry.");
        Status = "EXPIRED"; UpdatedAt = now;
    }
    public void ConfirmPaid(string endToEndId, decimal amount, DateTimeOffset paidAt)
    {
        if (amount != GrossAmount || string.IsNullOrWhiteSpace(endToEndId)) throw new InvalidOperationException("Provider confirmation does not match the payment.");
        if (Status == "PAID") { if (EndToEndId != endToEndId) throw new InvalidOperationException("A different confirmation already paid this payment."); return; }
        if (Status is not ("PENDING" or "PROCESSING")) throw new InvalidOperationException("This payment cannot be confirmed from its current state.");
        EndToEndId = endToEndId.Trim(); PaidAt = paidAt; Status = "PAID"; UpdatedAt = paidAt;
    }
}

public sealed class PaymentWebhookEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Provider { get; set; } = "PUSHINPAY";
    public string ExternalEventId { get; set; } = "";
    public Guid? PaymentId { get; set; }
    public string EventType { get; set; } = "";
    public string Status { get; set; } = "RECEIVED";
    public string PayloadHash { get; set; } = "";
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
}
