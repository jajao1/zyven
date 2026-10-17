using System.Security.Claims;
using Zyven.Application;
using Zyven.Infrastructure;
namespace Zyven.Api;

public static class PixRecoveryEndpoints { public static void MapPixRecovery(this WebApplication app) { var g = app.MapGroup("/api/organizations/{org:guid}/offers/{offer:guid}/pix-recovery").RequireAuthorization(); g.MapGet("", (Guid org, Guid offer, ClaimsPrincipal u, PixRecoverySettingsService s, CancellationToken ct) => Run(async () => Results.Ok(await s.Get(org, offer, User(u), ct)))); g.MapPut("", (Guid org, Guid offer, PixRecoveryRequest r, ClaimsPrincipal u, PixRecoverySettingsService s, CancellationToken ct) => Run(async () => Results.Ok(await s.Save(org, offer, User(u), r, ct)))); g.MapPost("/publish", (Guid org, Guid offer, ClaimsPrincipal u, PixRecoverySettingsService s, CancellationToken ct) => Run(async () => Results.Ok(await s.Publish(org, offer, User(u), ct)))); } private static Guid User(ClaimsPrincipal u) => Guid.Parse(u.FindFirstValue("sub")!); private static async Task<IResult> Run(Func<Task<IResult>> a) { try { return await a(); } catch (OrganizationException e) { return Results.Problem(statusCode: e.Status, title: e.Message); } } }
