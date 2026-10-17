using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Zyven.Application;
using Zyven.Domain;
namespace Zyven.Infrastructure;

public sealed record StoredDownload(Stream Stream, string ContentType, string Name);

public sealed class DigitalFileService(ZyvenDbContext db, TenantAuthorization tenants, IPrivateFileStore files, TimeProvider time)
{
    public const long MaxSize = 25 * 1024 * 1024;
    public async Task<DigitalFileResponse> Save(Guid org, Guid offer, Guid user, string name, string declaredType, long size, Stream source, CancellationToken ct)
    {
        var member = await tenants.RequireMembership(org, user, ct);
        if (member.Role is not (OrganizationRoles.Owner or OrganizationRoles.Admin or OrganizationRoles.Operator)) throw new OrganizationException(403, "Seu papel não permite configurar a entrega.");
        if (size is <= 0 or > MaxSize || !await db.Offers.AnyAsync(x => x.Id == offer && x.OrganizationId == org, ct)) throw new OrganizationException(400, "Arquivo inválido ou maior que 25 MiB.");
        await using var memory = new MemoryStream(); await source.CopyToAsync(memory, ct);
        if (memory.Length != size) throw new OrganizationException(400, "O arquivo enviado está incompleto.");
        var contentType = Detect(memory.GetBuffer().AsSpan(0, (int)Math.Min(memory.Length, 8)), declaredType) ?? throw new OrganizationException(400, "Envie PDF, ZIP, EPUB, planilha, PNG ou JPEG válido.");
        var safeName = Path.GetFileName(name.Trim()); if (safeName.Length is 0 or > 180) throw new OrganizationException(400, "Nome de arquivo inválido.");
        var hash = Convert.ToHexString(SHA256.HashData(memory.ToArray())); memory.Position = 0; var key = await files.Save(memory, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var asset = new DigitalAsset { OrganizationId = org, DisplayName = safeName, ContentType = contentType, Size = size, Sha256 = hash, StorageKey = key, CreatedAt = time.GetUtcNow() }; db.DigitalAssets.Add(asset);
        var definition = await db.FulfillmentDefinitions.SingleOrDefaultAsync(x => x.OrganizationId == org && x.OfferId == offer && x.Type == "DIGITAL_FILE", ct);
        if (definition is null) { definition = FulfillmentDefinition.DigitalFile(org, offer, asset.Id, safeName, time.GetUtcNow()); db.FulfillmentDefinitions.Add(definition); } else definition.UpdateDigitalFile(asset.Id, safeName, time.GetUtcNow());
        await db.SaveChangesAsync(ct);
        var entitlements = await db.Entitlements.Where(x => x.OrganizationId == org && x.OfferId == offer && x.Status == "ACTIVE").ToListAsync(ct);
        var delivered = await db.FulfillmentExecutions.Where(x => x.FulfillmentDefinitionId == definition.Id).Select(x => x.EntitlementId).ToListAsync(ct);
        foreach (var entitlement in entitlements.Where(x => !delivered.Contains(x.Id))) db.FulfillmentExecutions.Add(FulfillmentExecution.Deliver(entitlement, definition, time.GetUtcNow()));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new(asset.Id, "DIGITAL_FILE", asset.DisplayName, asset.ContentType, asset.Size, "ACTIVE");
    }
    public async Task<DigitalFileResponse> Get(Guid org, Guid offer, Guid user, CancellationToken ct)
    {
        await tenants.RequireMembership(org, user, ct);
        return await (from d in db.FulfillmentDefinitions.AsNoTracking() join a in db.DigitalAssets.AsNoTracking() on d.DigitalAssetId equals a.Id where d.OrganizationId == org && d.OfferId == offer && d.Type == "DIGITAL_FILE" select new DigitalFileResponse(a.Id, d.Type, d.Name, a.ContentType, a.Size, d.Status)).SingleOrDefaultAsync(ct) ?? throw new OrganizationException(404, "Arquivo não configurado.");
    }
    public async Task<StoredDownload?> BuyerDownload(string email, Guid executionId, CancellationToken ct) => await Authorized(email, executionId, ct);
    public async Task<StoredDownload?> CheckoutDownload(Guid checkoutId, string? secret, Guid executionId, CancellationToken ct)
    {
        if (secret is null || secret.Length != 96) return null;
        var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(secret)));
        var email = await db.Checkouts.AsNoTracking().Where(x => x.Id == checkoutId && x.AccessHash == hash && x.Status == "COMPLETED" && x.CreatedAt > time.GetUtcNow().AddDays(-30)).Select(x => x.Email).SingleOrDefaultAsync(ct);
        return email is null ? null : await Authorized(email.Trim().ToUpperInvariant(), executionId, ct);
    }
    private async Task<StoredDownload?> Authorized(string email, Guid executionId, CancellationToken ct)
    {
        var row = await (from x in db.FulfillmentExecutions.AsNoTracking() join e in db.Entitlements.AsNoTracking() on x.EntitlementId equals e.Id join p in db.Payments.AsNoTracking() on e.PaymentId equals p.Id join c in db.Customers.AsNoTracking() on p.CustomerId equals c.Id join a in db.DigitalAssets.AsNoTracking() on x.DigitalAssetId equals a.Id where x.Id == executionId && x.Type == "DIGITAL_FILE" && x.Status == "COMPLETED" && e.Status == "ACTIVE" && p.Status == "PAID" && c.NormalizedEmail == email select a).SingleOrDefaultAsync(ct);
        return row is null ? null : new(await files.Open(row.StorageKey, ct), row.ContentType, row.DisplayName);
    }
    private static string? Detect(ReadOnlySpan<byte> h, string declared) => h.Length >= 4 && h[0] == 0x25 && h[1] == 0x50 && h[2] == 0x44 && h[3] == 0x46 && declared.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) ? "application/pdf" : h.Length >= 4 && h[0] == 0x50 && h[1] == 0x4B && h[2] == 0x03 && h[3] == 0x04 && new[] { "application/zip", "application/epub+zip", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" }.Contains(declared, StringComparer.OrdinalIgnoreCase) ? declared.ToLowerInvariant() : h.Length >= 4 && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47 && declared.Equals("image/png", StringComparison.OrdinalIgnoreCase) ? "image/png" : h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF && declared.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) ? "image/jpeg" : null;
}
