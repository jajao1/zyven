using System.Security.Claims;
using Zyven.Application;
using Zyven.Infrastructure;
namespace Zyven.Api;

public static class OrganizationEndpoints
{
    public static void MapOrganizations(this WebApplication app)
    {
        var group = app.MapGroup("/api/organizations").RequireAuthorization();
        group.MapGet("/", (int? page, int? pageSize, OrganizationService service, ClaimsPrincipal user, CancellationToken ct) => Execute(async () => Results.Ok(await service.List(UserId(user), page ?? 1, pageSize ?? 20, ct))));
        group.MapPost("/", (OrganizationRequest input, OrganizationService service, ClaimsPrincipal user, CancellationToken ct) => Execute(async () =>
        {
            var result = await service.Create(UserId(user), input, ct);
            return Results.Created($"/api/organizations/{result.Id}", result);
        }));
        group.MapGet("/{id:guid}", (Guid id, OrganizationService service, ClaimsPrincipal user, CancellationToken ct) => Execute(async () => Results.Ok(await service.Get(id, UserId(user), ct))));
        group.MapPatch("/{id:guid}", (Guid id, OrganizationRequest input, OrganizationService service, ClaimsPrincipal user, CancellationToken ct) => Execute(async () => Results.Ok(await service.Rename(id, UserId(user), input, ct))));
        group.MapGet("/{id:guid}/members", (Guid id, int? page, int? pageSize, OrganizationService service, ClaimsPrincipal user, CancellationToken ct) => Execute(async () => Results.Ok(await service.Members(id, UserId(user), page ?? 1, pageSize ?? 20, ct))));
        group.MapPost("/{id:guid}/members", (Guid id, AddMemberRequest input, OrganizationService service, ClaimsPrincipal user, CancellationToken ct) => Execute(async () =>
        {
            var result = await service.Add(id, UserId(user), input, ct);
            return Results.Created($"/api/organizations/{id}/members/{result.Id}", result);
        }));
        group.MapPatch("/{id:guid}/members/{memberId:guid}", (Guid id, Guid memberId, ChangeRoleRequest input, OrganizationService service, ClaimsPrincipal user, CancellationToken ct) => Execute(async () => Results.Ok(await service.Change(id, memberId, UserId(user), input, ct))));
        group.MapDelete("/{id:guid}/members/{memberId:guid}", (Guid id, Guid memberId, OrganizationService service, ClaimsPrincipal user, CancellationToken ct) => Execute(async () =>
        {
            await service.Change(id, memberId, UserId(user), null, ct); return Results.NoContent();
        }));
    }
    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue("sub")!);
    private static async Task<IResult> Execute(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (OrganizationException error) { return Results.Problem(statusCode: error.Status, title: error.Message); }
    }
}
