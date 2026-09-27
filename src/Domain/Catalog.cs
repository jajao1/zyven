namespace Zyven.Domain;

public sealed class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Description { get; set; } = "";
    public string? ImageUrl { get; set; }
    public string Status { get; set; } = "DRAFT";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
public sealed class Offer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid ProductId { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Headline { get; set; } = "";
    public string Description { get; set; } = "";
    [System.Text.Json.Serialization.JsonNumberHandling(System.Text.Json.Serialization.JsonNumberHandling.WriteAsString)]
    public decimal Price { get; set; }
    public string Currency { get; set; } = "BRL";
    public string Status { get; set; } = "DRAFT";
    public string BillingType { get; set; } = "ONE_TIME";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
