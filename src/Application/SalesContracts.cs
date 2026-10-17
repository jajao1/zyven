namespace Zyven.Application;

public sealed record SalesSummaryResponse(
    string Currency,
    string AvailableBalance,
    string TotalReceived,
    string TotalFees,
    string NetPaid,
    int TotalPayments,
    int PaidPayments,
    int PendingPayments,
    int ExpiredPayments,
    int FailedPayments);

public sealed record SaleListItemResponse(
    Guid Id,
    string Status,
    string CustomerName,
    string CustomerEmail,
    string OfferName,
    string PaymentMethod,
    string? Provider,
    string Currency,
    string GrossAmount,
    string PlatformFee,
    string ProviderFee,
    string NetAmount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt);

public sealed record SaleDetailResponse(
    Guid Id,
    Guid CheckoutId,
    Guid CustomerId,
    Guid OfferId,
    string Status,
    string CustomerName,
    string CustomerEmail,
    string OfferName,
    string PaymentMethod,
    string? Provider,
    string Currency,
    string GrossAmount,
    string PlatformFee,
    string ProviderFee,
    string NetAmount,
    string ExternalReference,
    string? ProviderTransactionId,
    string? EndToEndId,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? PaidAt,
    string? EntitlementStatus,
    string? FulfillmentStatus);
