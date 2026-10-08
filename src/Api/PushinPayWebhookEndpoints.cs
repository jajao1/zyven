using System.Text.Json;
using Zyven.Infrastructure;

namespace Zyven.Api;

public static class PushinPayWebhookEndpoints
{
    public static void MapPushinPayWebhook(this WebApplication app)
    {
        app.MapPost("/api/webhooks/pushinpay/{merchantId:guid}/{secret}", async (Guid merchantId, string secret, HttpContext context, PushinPayWebhookService service, CancellationToken ct) =>
        {
            if (context.Request.ContentLength is > 131072) return Results.BadRequest();
            await using var buffer = new MemoryStream();
            await context.Request.Body.CopyToAsync(buffer, ct);
            if (buffer.Length is 0 or > 131072) return Results.BadRequest();
            try
            {
                var result = await service.Apply(merchantId, secret, buffer.ToArray(), ct);
                return Results.Ok(new { status = result.ToString().ToUpperInvariant() });
            }
            catch (JsonException) { return Results.BadRequest(); }
        });
    }
}
