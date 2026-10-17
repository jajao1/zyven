using System.Security.Claims;
using System.Text.Json;
using Zyven.Application;
using Zyven.Infrastructure;
namespace Zyven.Api;

public static class TelegramEndpoints
{
    public static void MapTelegram(this WebApplication app)
    {
        var org = app.MapGroup("/api/organizations/{org:guid}/telegram").RequireAuthorization();
        org.MapGet("", (Guid org, ClaimsPrincipal user, TelegramIntegrationService service, CancellationToken ct) => Run(async () => Results.Ok(await service.Get(org, User(user), ct))));
        org.MapPut("", (Guid org, TelegramConnectRequest input, ClaimsPrincipal user, TelegramIntegrationService service, CancellationToken ct) => Run(async () => Results.Ok(await service.Connect(org, User(user), input, ct))));
        var fulfillment = app.MapGroup("/api/organizations/{org:guid}/offers/{offer:guid}/fulfillments").RequireAuthorization();
        fulfillment.MapGet("/telegram", (Guid org, Guid offer, ClaimsPrincipal user, TelegramIntegrationService service, CancellationToken ct) => Run(async () => Results.Ok(await service.GetFulfillment(org, offer, User(user), ct))));
        fulfillment.MapPut("/telegram", (Guid org, Guid offer, TelegramFulfillmentRequest input, ClaimsPrincipal user, TelegramIntegrationService service, CancellationToken ct) => Run(async () => Results.Ok(await service.SaveFulfillment(org, offer, User(user), input, ct))));
        app.MapPost("/api/webhooks/telegram/{integrationId:guid}/{secret}", async (Guid integrationId, string secret, HttpContext context, TelegramFulfillmentService service, CancellationToken ct) => await Run(async () => { using var json = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: ct); await service.ProcessWebhook(integrationId, secret, context.Request.Headers["X-Telegram-Bot-Api-Secret-Token"].ToString(), json, ct); return Results.Ok(); }));
    }
    private static Guid User(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue("sub")!);
    private static async Task<IResult> Run(Func<Task<IResult>> action) { try { return await action(); } catch (OrganizationException e) { return Results.Problem(statusCode: e.Status, title: e.Message); } }
}
