using Zyven.Domain;
namespace Zyven.Application;

public enum PaymentOperationError { Unavailable, Unsupported, Rejected, NotFound, Indeterminate }
public sealed record PaymentCapabilities(bool Pix, bool Card, bool Cancellation);
public sealed record PaymentPayer(string Name, string Document, string Email, string Phone);
// Created from a validated server-side payment snapshot, never bound from public JSON.
public sealed class PaymentChargeRequest
{
    public Guid PaymentId { get; }
    public Guid OrganizationId { get; }
    public Guid MerchantAccountId { get; }
    public string IdempotencyReference { get; } = "";
    public PaymentAmounts Amounts { get; } = null!;
    public string Currency { get; } = "";
    public DateTimeOffset ExpiresAt { get; }
    public PaymentPayer? Payer { get; }
    public string? ProviderRecipientId { get; }
    public PaymentChargeRequest(Payment payment, PaymentPayer? payer = null, string? providerRecipientId = null)
    {
        PaymentId = payment.Id; OrganizationId = payment.OrganizationId; MerchantAccountId = payment.MerchantAccountId;
        IdempotencyReference = payment.ExternalReference; Currency = payment.Currency; ExpiresAt = payment.ExpiresAt;
        Amounts = new PaymentAmounts(payment.GrossAmount, payment.DiscountAmount, payment.OrderBumpAmount, payment.PlatformFee, payment.ProviderFee);
        Payer = payer; ProviderRecipientId = providerRecipientId;
    }
}
public sealed record CardChargeRequest(PaymentChargeRequest Charge, string TokenReference);
public sealed record PaymentLookup(Guid MerchantAccountId, string Provider, string ExternalReference, string? ProviderTransactionId);
public sealed record PaymentProviderState(string ProviderTransactionId, string Status, decimal GrossAmount, string Currency, DateTimeOffset? PaidAt, DateTimeOffset? ExpiresAt, string? EndToEndId, string? PixCode, string? QrCodeData);
public sealed record PaymentOperationResult(PaymentProviderState? State, PaymentOperationError? Error);
public interface IPixProvider
{
    Task<PaymentOperationResult> CreatePixAsync(PaymentChargeRequest request, CancellationToken ct);
}
public interface ICardProvider
{
    Task<PaymentOperationResult> CreateCardAsync(CardChargeRequest request, CancellationToken ct);
}
public interface IPaymentProcessor : IPixProvider, ICardProvider
{
    PaymentCapabilities Capabilities { get; }
    Task<PaymentOperationResult> QueryAsync(PaymentLookup request, CancellationToken ct);
    Task<PaymentOperationResult> CancelAsync(PaymentLookup request, CancellationToken ct);
}
