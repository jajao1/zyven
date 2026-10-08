using System.Security.Claims;
using Zyven.Application;
using Zyven.Infrastructure;

namespace Zyven.Api;

public static class SalesEndpoints
{
    public static void MapSales(this WebApplication app)
    {
        var group = app.MapGroup("/api/organizations/{organizationId:guid}/finance").RequireAuthorization();
        group.MapGet("/summary", (Guid organizationId, SalesService service, ClaimsPrincipal user, CancellationToken ct) => Execute(async () => Results.Ok(await service.Summary(organizationId, UserId(user), ct))));
        group.MapGet("/sales", (Guid organizationId, int? page, int? pageSize, string? status, SalesService service, ClaimsPrincipal user, CancellationToken ct) => Execute(async () => Results.Ok(await service.List(organizationId, UserId(user), page ?? 1, pageSize ?? 20, status, ct))));
        group.MapGet("/sales/{paymentId:guid}", (Guid organizationId, Guid paymentId, SalesService service, ClaimsPrincipal user, CancellationToken ct) => Execute(async () => Results.Ok(await service.Detail(organizationId, paymentId, UserId(user), ct))));
    }

    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue("sub")!);
    private static async Task<IResult> Execute(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (OrganizationException error) { return Results.Problem(statusCode: error.Status, title: error.Message); }
    }
}
