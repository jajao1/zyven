namespace Zyven.Domain;

public sealed class PixRecoveryConfiguration
{
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid OrganizationId { get; set; }
    public Guid OfferId { get; set; }
    public bool Enabled { get; set; }
    public int PublishedVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}
public sealed class PixRecoveryMessage
{
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid OrganizationId { get; set; }
    public Guid ConfigurationId { get; set; }
    public int Version { get; set; }
    public int Position { get; set; }
    public int DelayMinutes { get; set; }
    public string EmailText { get; set; } = ""; public string TelegramText { get; set; } = ""; public DateTimeOffset CreatedAt { get; set; }
}
public sealed class PixRecoveryExecution
{
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid OrganizationId { get; set; }
    public Guid ConfigurationId { get; set; }
    public Guid PaymentId { get; set; }
    public int Version { get; set; }
    public int CurrentPosition { get; set; }
    public string Channel { get; set; } = "EMAIL"; public string Status { get; set; } = "SCHEDULED";
    public DateTimeOffset NextRunAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
public sealed class PixRecoveryAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid OrganizationId { get; set; }
    public Guid ExecutionId { get; set; }
    public int Position { get; set; }
    public int TryCount { get; set; }
    public string Channel { get; set; } = "EMAIL"; public string Status { get; set; } = "RESERVED";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
}
public sealed class BotCheckoutReference
{
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid OrganizationId { get; set; }
    public Guid OfferId { get; set; }
    public Guid TelegramIntegrationId { get; set; }
    public long TelegramUserId { get; set; }
    public string Hash { get; set; } = ""; public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public Guid? CheckoutId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
public sealed class BotCheckoutBinding { public Guid CheckoutId { get; set; } public Guid OrganizationId { get; set; } public Guid TelegramIntegrationId { get; set; } public long TelegramUserId { get; set; } public DateTimeOffset CreatedAt { get; set; } }
