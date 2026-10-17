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
        admin.MapGet("/digital-file", (Guid org, Guid offer, ClaimsPrincipal user, DigitalFileService service, CancellationToken ct) => Execute(async () => Results.Ok(await service.Get(org, offer, UserId(user), ct))));
        admin.MapPost("/digital-file", async (Guid org, Guid offer, IFormFile file, ClaimsPrincipal user, DigitalFileService service, CancellationToken ct) => await Execute(async () => Results.Ok(await service.Save(org, offer, UserId(user), file.FileName, file.ContentType, file.Length, file.OpenReadStream(), ct)))).DisableAntiforgery();
        app.MapGet("/api/public/checkouts/{id:guid}/delivery", (Guid id, HttpContext context, FulfillmentService service, CancellationToken ct) => Execute(async () => Results.Ok(await service.Delivery(id, context.Request.Cookies["zyven_checkout"], ct))));
        app.MapGet("/api/public/checkouts/{id:guid}/files/{executionId:guid}", async (Guid id, Guid executionId, HttpContext context, DigitalFileService service, CancellationToken ct) =>
        {
            var download = await service.CheckoutDownload(id, context.Request.Cookies["zyven_checkout"], executionId, ct);
            return download is null ? Results.NotFound() : Results.File(download.Stream, download.ContentType, download.Name, enableRangeProcessing: true);
        });
    }
    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue("sub")!);
    private static async Task<IResult> Execute(Func<Task<IResult>> action) { try { return await action(); } catch (OrganizationException error) { return Results.Problem(statusCode: error.Status, title: error.Message); } }
}
