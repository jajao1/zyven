namespace Zyven.Application;

public sealed record OrganizationRequest(string? Name);
public sealed record AddMemberRequest(string? Email, string? Role);
public sealed record ChangeRoleRequest(string? Role);
public sealed record OrganizationResponse(Guid Id, string Name, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string Role);
public sealed record MemberResponse(Guid Id, Guid UserId, string Email, string DisplayName, string Role, DateTimeOffset CreatedAt);
public sealed class OrganizationException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

public sealed record PageResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
