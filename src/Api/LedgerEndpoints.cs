using System.Security.Claims;
using Zyven.Application;
using Zyven.Infrastructure;
namespace Zyven.Api;

public static class LedgerEndpoints
{
    public static void MapLedger(this WebApplication app)
    {
        var group = app.MapGroup("/api/organizations/{organizationId:guid}/finance").RequireAuthorization();
        group.MapGet("/wallet", (Guid organizationId, LedgerService service, ClaimsPrincipal user, CancellationToken ct) => Execute(async () => Results.Ok(await service.Wallet(organizationId, UserId(user), ct))));
        group.MapGet("/ledger", (Guid organizationId, int? page, int? pageSize, LedgerService service, ClaimsPrincipal user, CancellationToken ct) => Execute(async () => Results.Ok(await service.History(organizationId, UserId(user), page ?? 1, pageSize ?? 20, ct))));
    }

    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue("sub")!);
    private static async Task<IResult> Execute(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (OrganizationException error) { return Results.Problem(statusCode: error.Status, title: error.Message); }
    }
}
