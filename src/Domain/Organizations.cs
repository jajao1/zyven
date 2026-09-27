namespace Zyven.Domain;

public sealed class Organization
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class OrganizationMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Role { get; set; } = OrganizationRoles.Operator;
    public DateTimeOffset CreatedAt { get; set; }
}

public static class OrganizationRoles
{
    public const string Owner = "OWNER", Admin = "ADMIN", Operator = "OPERATOR", Finance = "FINANCE", Support = "SUPPORT";
    public static bool IsValid(string? role) => role is Owner or Admin or Operator or Finance or Support;
    public static bool CanRename(string role) => role is Owner or Admin;
    public static bool CanManage(string actor, string target) => actor == Owner || actor == Admin && target is Operator or Finance or Support;
}

public sealed class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid ActorUserId { get; set; }
    public Guid TargetId { get; set; }
    public string Action { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
}
