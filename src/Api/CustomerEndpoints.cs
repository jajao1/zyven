using System.Security.Claims;
using Zyven.Application;
using Zyven.Infrastructure;
namespace Zyven.Api;

public static class CustomerEndpoints
{
    public static void MapCustomers(this WebApplication app)
    {
        var group = app.MapGroup("/api/organizations/{org:guid}/customers").RequireAuthorization();
        group.MapGet("/", (Guid org, int? page, int? pageSize, CustomerService service, ClaimsPrincipal user, CancellationToken ct) => Execute(async () => Results.Ok(await service.List(org, Guid.Parse(user.FindFirstValue("sub")!), page ?? 1, pageSize ?? 20, ct))));
        group.MapGet("/{id:guid}", (Guid org, Guid id, CustomerService service, ClaimsPrincipal user, CancellationToken ct) => Execute(async () => Results.Ok(await service.Detail(org, id, Guid.Parse(user.FindFirstValue("sub")!), ct))));
    }
    private static async Task<IResult> Execute(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (OrganizationException error) { return Results.Problem(statusCode: error.Status, title: error.Message); }
    }
}
