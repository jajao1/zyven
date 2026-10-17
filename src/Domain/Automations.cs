namespace Zyven.Domain;

public static class AutomationStepTypes
{
    public static readonly string[] All = ["SEND_TELEGRAM_MESSAGE", "SEND_OFFER", "CHECKOUT", "CONDITION", "WAIT", "TELEGRAM_ACCESS", "END"];
    public static bool IsValid(string value) => All.Contains(value, StringComparer.Ordinal);
}

public sealed class Automation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid OfferId { get; set; }
    public int PublishedVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}

public sealed class AutomationStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid AutomationId { get; set; }
    public int Version { get; set; }
    public int Position { get; set; }
    public string Type { get; set; } = "SEND_TELEGRAM_MESSAGE";
    public string ConfigurationJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class AutomationExecution
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid AutomationId { get; set; }
    public Guid EntitlementId { get; set; }
    public int Version { get; set; }
    public int CurrentPosition { get; set; }
    public long TelegramUserId { get; set; }
    public string Status { get; set; } = "RUNNING";
    public DateTimeOffset? NextRunAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
