using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Zyven.Application;
using Zyven.Domain;
namespace Zyven.Infrastructure;

public sealed record TelegramOptions(string PublicApiBaseUrl);
public sealed class TelegramIntegrationService(ZyvenDbContext db, TenantAuthorization tenants, TelegramCredentialVault vault, ITelegramClient telegram, TimeProvider time, TelegramOptions options)
{
    public async Task<TelegramIntegrationResponse> Connect(Guid org, Guid user, TelegramConnectRequest request, CancellationToken ct)
    {
        var member = await tenants.RequireMembership(org, user, ct); if (member.Role is not (OrganizationRoles.Owner or OrganizationRoles.Admin)) throw new OrganizationException(403, "Somente proprietário ou administrador pode conectar o bot.");
        if (string.IsNullOrWhiteSpace(request.Token) || request.Token.Length > 256) throw new OrganizationException(400, "Informe um token válido.");
        var bot = await telegram.GetMe(request.Token.Trim(), ct); var encrypted = vault.Encrypt(request.Token.Trim()); var rawSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var item = await db.TelegramIntegrations.SingleOrDefaultAsync(x => x.OrganizationId == org, ct);
        if (item is null) { item = new() { OrganizationId = org, CreatedAt = time.GetUtcNow() }; db.TelegramIntegrations.Add(item); }
        item.BotId = bot.Id; item.BotName = bot.FirstName; item.BotUsername = bot.Username; item.TokenCiphertext = encrypted.Ciphertext; item.TokenNonce = encrypted.Nonce; item.TokenTag = encrypted.Tag; item.TokenFingerprint = encrypted.Fingerprint; item.WebhookSecretHash = TelegramCredentialVault.Hash(rawSecret); item.Status = "ACTIVE"; item.UpdatedAt = time.GetUtcNow();
        var baseUrl = options.PublicApiBaseUrl; if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https") throw new OrganizationException(503, "Configure uma URL pública HTTPS para o Telegram.");
        await telegram.SetWebhook(request.Token.Trim(), $"{baseUrl!.TrimEnd('/')}/api/webhooks/telegram/{item.Id}/{rawSecret}", rawSecret, ct); await db.SaveChangesAsync(ct);
        return Response(item);
    }
    public async Task<TelegramIntegrationResponse> Get(Guid org, Guid user, CancellationToken ct) { await tenants.RequireMembership(org, user, ct); return Response(await db.TelegramIntegrations.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == org, ct) ?? throw new OrganizationException(404, "Bot Telegram não conectado.")); }
    public async Task<TelegramFulfillmentResponse> SaveFulfillment(Guid org, Guid offer, Guid user, TelegramFulfillmentRequest request, CancellationToken ct)
    {
        var member = await tenants.RequireMembership(org, user, ct); if (member.Role is not (OrganizationRoles.Owner or OrganizationRoles.Admin or OrganizationRoles.Operator)) throw new OrganizationException(403, "Seu papel não permite configurar a entrega.");
        if (!await db.Offers.AnyAsync(x => x.Id == offer && x.OrganizationId == org, ct)) throw new OrganizationException(404, "Oferta não encontrada.");
        var integration = await db.TelegramIntegrations.SingleOrDefaultAsync(x => x.OrganizationId == org && x.Status == "ACTIVE", ct) ?? throw new OrganizationException(409, "Conecte um bot Telegram primeiro.");
        var token = vault.Decrypt(new(integration.TokenCiphertext, integration.TokenNonce, integration.TokenTag, integration.TokenFingerprint)); var chat = await telegram.GetChat(token, request.ChatId, ct);
        if (chat.Type is not ("group" or "supergroup" or "channel") || !await telegram.IsAdministrator(token, chat.Id, integration.BotId, ct)) throw new OrganizationException(400, "O bot precisa ser administrador do grupo ou canal.");
        var definition = await db.FulfillmentDefinitions.SingleOrDefaultAsync(x => x.OrganizationId == org && x.OfferId == offer && x.Type == "TELEGRAM_ACCESS", ct);
        if (definition is null) { definition = FulfillmentDefinition.Telegram(org, offer, integration.Id, chat.Id, request.Name, time.GetUtcNow()); db.FulfillmentDefinitions.Add(definition); } else definition.UpdateTelegram(integration.Id, chat.Id, request.Name, time.GetUtcNow());
        var entitlements = await db.Entitlements.Where(x => x.OrganizationId == org && x.OfferId == offer && x.Status == "ACTIVE").ToListAsync(ct); var delivered = await db.FulfillmentExecutions.Where(x => x.FulfillmentDefinitionId == definition.Id).Select(x => x.EntitlementId).ToListAsync(ct); foreach (var e in entitlements.Where(x => !delivered.Contains(x.Id))) db.FulfillmentExecutions.Add(FulfillmentExecution.Deliver(e, definition, time.GetUtcNow()));
        await db.SaveChangesAsync(ct); return new(definition.Id, definition.Type, definition.Name, definition.TelegramChatId, integration.BotUsername, definition.Status);
    }
    public async Task<TelegramFulfillmentResponse> GetFulfillment(Guid org, Guid offer, Guid user, CancellationToken ct)
    {
        await tenants.RequireMembership(org, user, ct);
        var row = await (from d in db.FulfillmentDefinitions.AsNoTracking()
                         join i in db.TelegramIntegrations.AsNoTracking() on d.TelegramIntegrationId equals i.Id
                         where d.OrganizationId == org && d.OfferId == offer && d.Type == "TELEGRAM_ACCESS"
                         select new { d, i }).SingleOrDefaultAsync(ct) ?? throw new OrganizationException(404, "Entrega Telegram não configurada.");
        return new(row.d.Id, row.d.Type, row.d.Name, row.d.TelegramChatId, row.i.BotUsername, row.d.Status);
    }
    private static TelegramIntegrationResponse Response(TelegramIntegration x) => new(x.Id, x.BotId, x.BotName, x.BotUsername, x.TokenFingerprint, x.Status);
}



