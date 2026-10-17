namespace Zyven.Application;

public sealed record BuyerCodeRequest(string Email);
public sealed record BuyerCodeVerification(string Email, string Code);
public sealed record BuyerProfileResponse(string Email);
public sealed record BuyerPurchaseItemResponse(Guid Id, string OfferName, string SellerName, string Currency, string Amount, DateTimeOffset PaidAt, string AccessStatus);
public sealed record BuyerPurchaseDetailResponse(Guid Id, string OfferName, string SellerName, string Currency, string Amount, DateTimeOffset PaidAt, IReadOnlyList<DeliveryItemResponse> Items);
