namespace Zyven.Application;

public sealed record ExternalLinkFulfillmentRequest(string Name, string Url);
public sealed record ExternalLinkFulfillmentResponse(Guid Id, string Type, string Name, string Url, string Status);
public sealed record DeliveryItemResponse(Guid Id, string Type, string Name, string Url, DateTimeOffset DeliveredAt);
public sealed record DeliveryResponse(Guid EntitlementId, string Status, IReadOnlyList<DeliveryItemResponse> Items);
