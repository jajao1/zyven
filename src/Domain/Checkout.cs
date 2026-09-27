namespace Zyven.Domain;

public sealed class OfferPage
{
    public Guid OfferId { get; set; }
    public Guid OrganizationId { get; set; }
    public string ContentJson { get; set; } = "{}";
    public DateTimeOffset UpdatedAt { get; set; }
}
public sealed class CheckoutSession
{
    public Guid CustomerId { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid OfferId { get; set; }
    public string AccessHash { get; set; } = "";
    public string Status { get; set; } = "CREATED";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string? Document { get; set; }
    public string FieldsJson { get; set; } = "{}";
    public string FormJson { get; set; } = "[]";
    public decimal Price { get; set; }
    public string Currency { get; set; } = "BRL";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
