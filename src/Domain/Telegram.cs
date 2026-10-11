namespace Zyven.Domain;

public sealed class TelegramIntegration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public long BotId { get; set; }
    public string BotName { get; set; } = "";
    public string BotUsername { get; set; } = "";
    public string TokenCiphertext { get; set; } = "";
    public string TokenNonce { get; set; } = "";
    public string TokenTag { get; set; } = "";
    public string TokenFingerprint { get; set; } = "";
    public string WebhookSecretHash { get; set; } = "";
    public string Status { get; set; } = "ACTIVE";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class TelegramLinkToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid FulfillmentExecutionId { get; set; }
    public string Hash { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class TelegramAccessGrant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid FulfillmentExecutionId { get; set; }
    public long TelegramUserId { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public DateTimeOffset GrantedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
