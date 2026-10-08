namespace Zyven.Domain;

public sealed class FulfillmentDefinition
{
    private FulfillmentDefinition() { }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OrganizationId { get; private set; }
    public Guid OfferId { get; private set; }
    public string Type { get; private set; } = "EXTERNAL_LINK";
    public string Name { get; private set; } = "";
    public string ExternalUrl { get; private set; } = "";
    public Guid? DigitalAssetId { get; private set; }
    public string Status { get; private set; } = "ACTIVE";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static FulfillmentDefinition ExternalLink(Guid organizationId, Guid offerId, string name, string url, DateTimeOffset now)
    {
        if (organizationId == Guid.Empty || offerId == Guid.Empty) throw new ArgumentException("Organization and offer are required.");
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100) throw new ArgumentException("A valid fulfillment name is required.", nameof(name));
        if (!SafeUrl(url)) throw new ArgumentException("A safe HTTPS URL is required.", nameof(url));
        return new() { OrganizationId = organizationId, OfferId = offerId, Name = name.Trim(), ExternalUrl = url.Trim(), CreatedAt = now, UpdatedAt = now };
    }

    public void UpdateExternalLink(string name, string url, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100 || !SafeUrl(url)) throw new ArgumentException("A valid name and HTTPS URL are required.");
        Name = name.Trim(); ExternalUrl = url.Trim(); Status = "ACTIVE"; UpdatedAt = now;
    }
    public static FulfillmentDefinition DigitalFile(Guid organizationId, Guid offerId, Guid assetId, string name, DateTimeOffset now)
    {
        if (organizationId == Guid.Empty || offerId == Guid.Empty || assetId == Guid.Empty || string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100) throw new ArgumentException("A valid digital asset is required.");
        return new() { OrganizationId = organizationId, OfferId = offerId, DigitalAssetId = assetId, Type = "DIGITAL_FILE", Name = name.Trim(), CreatedAt = now, UpdatedAt = now };
    }
    public void UpdateDigitalFile(Guid assetId, string name, DateTimeOffset now)
    {
        if (Type != "DIGITAL_FILE" || assetId == Guid.Empty || string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100) throw new ArgumentException("A valid digital asset is required.");
        DigitalAssetId = assetId; Name = name.Trim(); Status = "ACTIVE"; UpdatedAt = now;
    }
    private static bool SafeUrl(string? value) => value is { Length: <= 2048 } && Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo);
}

public sealed class Entitlement
{
    private Entitlement() { }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OrganizationId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid OfferId { get; private set; }
    public Guid PaymentId { get; private set; }
    public string Type { get; private set; } = "PURCHASE";
    public string Status { get; private set; } = "ACTIVE";
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static Entitlement FromPaidPayment(Payment payment, DateTimeOffset now)
    {
        if (payment.Status != "PAID" || payment.PaidAt is null) throw new InvalidOperationException("A paid payment is required.");
        return new() { OrganizationId = payment.OrganizationId, CustomerId = payment.CustomerId, OfferId = payment.OfferId, PaymentId = payment.Id, StartsAt = payment.PaidAt.Value, CreatedAt = now };
    }
}

public sealed class FulfillmentExecution
{
    private FulfillmentExecution() { }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OrganizationId { get; private set; }
    public Guid EntitlementId { get; private set; }
    public Guid FulfillmentDefinitionId { get; private set; }
    public string Type { get; private set; } = "EXTERNAL_LINK";
    public string Status { get; private set; } = "COMPLETED";
    public string Name { get; private set; } = "";
    public string ExternalUrl { get; private set; } = "";
    public Guid? DigitalAssetId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset CompletedAt { get; private set; }

    public static FulfillmentExecution Deliver(Entitlement entitlement, FulfillmentDefinition definition, DateTimeOffset now)
    {
        if (entitlement.Status != "ACTIVE" || definition.Status != "ACTIVE" || definition.Type is not ("EXTERNAL_LINK" or "DIGITAL_FILE") || entitlement.OrganizationId != definition.OrganizationId || entitlement.OfferId != definition.OfferId || (definition.Type == "DIGITAL_FILE" && definition.DigitalAssetId is null)) throw new InvalidOperationException("An active matching entitlement and fulfillment are required.");
        return new() { OrganizationId = entitlement.OrganizationId, EntitlementId = entitlement.Id, FulfillmentDefinitionId = definition.Id, Type = definition.Type, Name = definition.Name, ExternalUrl = definition.ExternalUrl, DigitalAssetId = definition.DigitalAssetId, CreatedAt = now, CompletedAt = now };
    }
}

public sealed class DigitalAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public string DisplayName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Size { get; set; }
    public string Sha256 { get; set; } = "";
    public string StorageKey { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
