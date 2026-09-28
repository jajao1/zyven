using System.Security.Claims;
using Zyven.Application;
using Zyven.Infrastructure;
namespace Zyven.Api;

public static class FulfillmentEndpoints
{
    public static void MapFulfillment(this WebApplication app)
    {
        var admin = app.MapGroup("/api/organizations/{org:guid}/offers/{offer:guid}/fulfillments").RequireAuthorization();
        admin.MapGet("/external-link", (Guid org, Guid offer, ClaimsPrincipal user, FulfillmentService service, CancellationToken ct) => Execute(async () => Results.Ok(await service.GetExternalLink(org, offer, UserId(user), ct))));
        admin.MapPut("/external-link", (Guid org, Guid offer, ExternalLinkFulfillmentRequest input, ClaimsPrincipal user, FulfillmentService service, CancellationToken ct) => Execute(async () => Results.Ok(await service.SaveExternalLink(org, offer, UserId(user), input, ct))));
        app.MapGet("/api/public/checkouts/{id:guid}/delivery", (Guid id, HttpContext context, FulfillmentService service, CancellationToken ct) => Execute(async () => Results.Ok(await service.Delivery(id, context.Request.Cookies["zyven_checkout"], ct))));
    }
    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue("sub")!);
    private static async Task<IResult> Execute(Func<Task<IResult>> action) { try { return await action(); } catch (OrganizationException error) { return Results.Problem(statusCode: error.Status, title: error.Message); } }
}
