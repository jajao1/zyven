namespace Zyven.Domain;

public sealed class BuyerAccessCode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string NormalizedEmail { get; set; } = "";
    public string CodeHash { get; set; } = "";
    public int FailedAttempts { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class BuyerSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TokenHash { get; set; } = "";
    public string NormalizedEmail { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
