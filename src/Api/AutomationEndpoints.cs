using System.Security.Claims;
using Zyven.Application;
using Zyven.Infrastructure;
namespace Zyven.Api;

public static class AutomationEndpoints
{
    public static void MapAutomations(this WebApplication app)
    {
        var group = app.MapGroup("/api/organizations/{org:guid}/offers/{offer:guid}/automation").RequireAuthorization();
        group.MapGet("", (Guid org, Guid offer, ClaimsPrincipal user, AutomationService service, CancellationToken ct) => Run(async () => Results.Ok(await service.Get(org, offer, User(user), ct))));
        group.MapPut("", (Guid org, Guid offer, AutomationDraftRequest input, ClaimsPrincipal user, AutomationService service, CancellationToken ct) => Run(async () => Results.Ok(await service.Save(org, offer, User(user), input, ct))));
        group.MapPost("/publish", (Guid org, Guid offer, ClaimsPrincipal user, AutomationService service, CancellationToken ct) => Run(async () => Results.Ok(await service.Publish(org, offer, User(user), ct))));
    }
    private static Guid User(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue("sub")!);
    private static async Task<IResult> Run(Func<Task<IResult>> action) { try { return await action(); } catch (OrganizationException e) { return Results.Problem(statusCode: e.Status, title: e.Message); } }
}
