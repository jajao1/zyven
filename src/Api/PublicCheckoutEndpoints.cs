using System.Security.Claims;
using Zyven.Application;
using Zyven.Infrastructure;
namespace Zyven.Api;

public static class PublicCheckoutEndpoints
{
    public static void MapPublicCheckout(this WebApplication app)
    {
        var admin = app.MapGroup("/api/organizations/{org:guid}/offers/{offer:guid}/page").RequireAuthorization();
        admin.MapGet("", (Guid org, Guid offer, ClaimsPrincipal user, PublicCheckoutService service, CancellationToken ct) => Execute(async () => Results.Ok(await service.Page(org, offer, Guid.Parse(user.FindFirstValue("sub")!), ct))));
        admin.MapPut("", (Guid org, Guid offer, PageContent input, ClaimsPrincipal user, PublicCheckoutService service, CancellationToken ct) => Execute(async () => Results.Ok(await service.SavePage(org, offer, Guid.Parse(user.FindFirstValue("sub")!), input, ct))));
        app.MapGet("/api/public/offers/{slug}", (string slug, PublicCheckoutService service, CancellationToken ct) => Execute(async () => Results.Ok(await service.Public(slug, ct))));
        app.MapPost("/api/public/offers/{slug}/checkouts", (string slug, CheckoutInput input, HttpContext context, PublicCheckoutService service, TimeProvider time, CancellationToken ct) => Execute(async () =>
        {
            var grant = await service.Create(slug, input, ct);
            context.Response.Cookies.Append("zyven_checkout", grant.Secret, new() { HttpOnly = true, Secure = !app.Environment.IsDevelopment(), SameSite = SameSiteMode.Strict, Path = $"/api/public/checkouts/{grant.Response.Id}", Expires = time.GetUtcNow().AddDays(30), IsEssential = true });
            return Results.Created($"/api/public/checkouts/{grant.Response.Id}", grant.Response);
        }));
        app.MapGet("/api/public/checkouts/{id:guid}", (Guid id, HttpContext context, PublicCheckoutService service, CancellationToken ct) => Execute(async () => Results.Ok(await service.Read(id, context.Request.Cookies["zyven_checkout"], ct))));
        app.MapPatch("/api/public/checkouts/{id:guid}", (Guid id, CheckoutInput input, HttpContext context, PublicCheckoutService service, CancellationToken ct) => Execute(async () => Results.Ok(await service.Update(id, context.Request.Cookies["zyven_checkout"], input, ct))));
        app.MapPost("/api/public/checkouts/{id:guid}/payments/pix", (Guid id, HttpContext context, PixPaymentService service, CancellationToken ct) => Execute(async () => Results.Ok(await service.Create(id, context.Request.Cookies["zyven_checkout"], ct))));
        app.MapGet("/api/public/checkouts/{id:guid}/payments/pix", (Guid id, HttpContext context, PixPaymentService service, CancellationToken ct) => Execute(async () => Results.Ok(await service.Read(id, context.Request.Cookies["zyven_checkout"], ct))));
    }
    private static async Task<IResult> Execute(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (OrganizationException error) { return Results.Problem(statusCode: error.Status, title: error.Message); }
    }
}
