using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Zyven.Infrastructure;
namespace Zyven.Api;

public static class CelcoinWebhookEndpoints
{
    public static void MapCelcoinWebhook(this WebApplication app)
    {
        app.MapPost("/api/webhooks/celcoin", async (HttpContext context, CelcoinWebhookService service, IOptions<CelcoinOptions> configured, CancellationToken ct) =>
        {
            if (!ValidBasic(context.Request.Headers.Authorization, configured.Value.WebhookUsername, configured.Value.WebhookPassword)) { context.Response.Headers.WWWAuthenticate = "Basic realm=celcoin"; return Results.Unauthorized(); }
            await using var buffer = new MemoryStream(); await context.Request.Body.CopyToAsync(buffer, ct);
            if (buffer.Length is 0 or > 131072) return Results.BadRequest();
            try { var result = await service.Apply(buffer.ToArray(), ct); return Results.Ok(new { status = result.ToString().ToUpperInvariant() }); }
            catch (JsonException) { return Results.BadRequest(); }
        });
    }

    internal static bool ValidBasic(string? header, string username, string password)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || header is null || !header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            var supplied = Convert.FromBase64String(header[6..].Trim()); var expected = Encoding.UTF8.GetBytes(username + ":" + password);
            return supplied.Length == expected.Length && CryptographicOperations.FixedTimeEquals(supplied, expected);
        }
        catch (FormatException) { return false; }
    }
}
