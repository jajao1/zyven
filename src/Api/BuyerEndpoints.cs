using Zyven.Application;
using Zyven.Infrastructure;
namespace Zyven.Api;

public static class BuyerEndpoints
{
    private const string CookieName = "zyven_buyer";
    public static void MapBuyer(this WebApplication app)
    {
        var group = app.MapGroup("/api/buyer");
        group.MapPost("/auth/request-code", async (BuyerCodeRequest input, BuyerAuthService auth, CancellationToken ct) =>
        {
            await auth.RequestCode(input, ct);
            return Results.Accepted(value: new { message = "Se houver compras para este e-mail, enviaremos um código de acesso." });
        });
        group.MapPost("/auth/verify-code", async (BuyerCodeVerification input, BuyerAuthService auth, HttpContext context, IWebHostEnvironment env, CancellationToken ct) =>
        {
            var session = await auth.Verify(input, ct);
            if (session is null) return Results.Problem(statusCode: 401, title: "Código inválido ou expirado.");
            context.Response.Cookies.Append(CookieName, session.Value.Token, Options(env.IsDevelopment(), session.Value.ExpiresAt));
            return Results.Ok(new BuyerProfileResponse(input.Email.Trim().ToLowerInvariant()));
        });
        group.MapPost("/auth/logout", async (BuyerAuthService auth, HttpContext context, IWebHostEnvironment env, CancellationToken ct) =>
        {
            await auth.Logout(context.Request.Cookies[CookieName], ct);
            context.Response.Cookies.Delete(CookieName, Options(env.IsDevelopment(), null));
            return Results.NoContent();
        });
        group.MapGet("/me", async (BuyerAuthService auth, HttpContext context, CancellationToken ct) =>
        {
            var email = await auth.Authenticate(context.Request.Cookies[CookieName], ct);
            return email is null ? Results.Unauthorized() : Results.Ok(new BuyerProfileResponse(email.ToLowerInvariant()));
        });
        group.MapGet("/purchases", async (BuyerAuthService auth, BuyerPurchaseService purchases, HttpContext context, CancellationToken ct) =>
        {
            var email = await auth.Authenticate(context.Request.Cookies[CookieName], ct);
            return email is null ? Results.Unauthorized() : Results.Ok(await purchases.List(email, ct));
        });
        group.MapGet("/purchases/{paymentId:guid}", async (Guid paymentId, BuyerAuthService auth, BuyerPurchaseService purchases, HttpContext context, CancellationToken ct) =>
        {
            var email = await auth.Authenticate(context.Request.Cookies[CookieName], ct);
            if (email is null) return Results.Unauthorized();
            var item = await purchases.Detail(email, paymentId, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });
        group.MapGet("/files/{executionId:guid}", async (Guid executionId, BuyerAuthService auth, DigitalFileService files, HttpContext context, CancellationToken ct) =>
        {
            var email = await auth.Authenticate(context.Request.Cookies[CookieName], ct);
            if (email is null) return Results.Unauthorized();
            var download = await files.BuyerDownload(email, executionId, ct);
            return download is null ? Results.NotFound() : Results.File(download.Stream, download.ContentType, download.Name, enableRangeProcessing: true);
        });
    }

    private static CookieOptions Options(bool development, DateTimeOffset? expires) => new() { HttpOnly = true, Secure = !development, SameSite = SameSiteMode.Lax, Path = "/api/buyer", IsEssential = true, Expires = expires };
}
