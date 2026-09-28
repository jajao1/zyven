using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Zyven.Application;
using Zyven.Domain;
namespace Zyven.Infrastructure;

public sealed class FulfillmentService(ZyvenDbContext db, TenantAuthorization tenants, TimeProvider time)
{
    public async Task<ExternalLinkFulfillmentResponse> SaveExternalLink(Guid organizationId, Guid offerId, Guid userId, ExternalLinkFulfillmentRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (!await db.Organizations.FromSqlInterpolated($"SELECT * FROM \"Organizations\" WHERE \"Id\" = {organizationId} FOR UPDATE").AnyAsync(ct)) throw Missing();
        var member = await tenants.RequireMembership(organizationId, userId, ct);
        if (member.Role is not (OrganizationRoles.Owner or OrganizationRoles.Admin or OrganizationRoles.Operator)) throw new OrganizationException(403, "Seu papel não permite configurar a entrega.");
        if (!await db.Offers.AnyAsync(x => x.Id == offerId && x.OrganizationId == organizationId, ct)) throw Missing();
        var definition = await db.FulfillmentDefinitions.SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.OfferId == offerId && x.Type == "EXTERNAL_LINK", ct);
        try
        {
            if (definition is null) { definition = FulfillmentDefinition.ExternalLink(organizationId, offerId, request.Name, request.Url, time.GetUtcNow()); db.FulfillmentDefinitions.Add(definition); }
            else definition.UpdateExternalLink(request.Name, request.Url, time.GetUtcNow());
        }
        catch (ArgumentException) { throw new OrganizationException(400, "Informe um nome e uma URL HTTPS válida, sem credenciais embutidas."); }
        var entitled = await db.Entitlements.Where(x => x.OrganizationId == organizationId && x.OfferId == offerId && x.Status == "ACTIVE").ToListAsync(ct);
        var delivered = await db.FulfillmentExecutions.Where(x => x.OrganizationId == organizationId && x.FulfillmentDefinitionId == definition.Id).Select(x => x.EntitlementId).ToListAsync(ct);
        foreach (var entitlement in entitled.Where(x => !delivered.Contains(x.Id))) db.FulfillmentExecutions.Add(FulfillmentExecution.Deliver(entitlement, definition, time.GetUtcNow()));
        db.AuditLogs.Add(new() { OrganizationId = organizationId, ActorUserId = userId, TargetId = definition.Id, Action = "fulfillment.external_link_configured", OccurredAt = time.GetUtcNow() });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return DefinitionResponse(definition);
    }

    public async Task<ExternalLinkFulfillmentResponse> GetExternalLink(Guid organizationId, Guid offerId, Guid userId, CancellationToken ct)
    {
        await tenants.RequireMembership(organizationId, userId, ct);
        var definition = await db.FulfillmentDefinitions.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.OfferId == offerId && x.Type == "EXTERNAL_LINK", ct) ?? throw Missing();
        return DefinitionResponse(definition);
    }

    public async Task FulfillPaidPayment(Payment payment, CancellationToken ct)
    {
        var entitlement = await db.Entitlements.SingleOrDefaultAsync(x => x.PaymentId == payment.Id, ct);
        if (entitlement is null) { entitlement = Entitlement.FromPaidPayment(payment, time.GetUtcNow()); db.Entitlements.Add(entitlement); }
        var definitions = await db.FulfillmentDefinitions.Where(x => x.OrganizationId == payment.OrganizationId && x.OfferId == payment.OfferId && x.Status == "ACTIVE").ToListAsync(ct);
        var existing = await db.FulfillmentExecutions.Where(x => x.EntitlementId == entitlement.Id).Select(x => x.FulfillmentDefinitionId).ToListAsync(ct);
        foreach (var definition in definitions.Where(x => !existing.Contains(x.Id))) db.FulfillmentExecutions.Add(FulfillmentExecution.Deliver(entitlement, definition, time.GetUtcNow()));
    }

    public async Task<DeliveryResponse> Delivery(Guid checkoutId, string? secret, CancellationToken ct)
    {
        if (secret is null || secret.Length != 96) throw MissingDelivery();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
        var accessCutoff = time.GetUtcNow().AddDays(-30);
        var checkout = await db.Checkouts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == checkoutId && x.AccessHash == hash && x.Status == "COMPLETED" && x.CreatedAt > accessCutoff, ct) ?? throw MissingDelivery();
        var entitlement = await (from item in db.Entitlements.AsNoTracking()
                                 join payment in db.Payments.AsNoTracking() on item.PaymentId equals payment.Id
                                 where payment.CheckoutSessionId == checkoutId && item.OrganizationId == checkout.OrganizationId && item.Status == "ACTIVE"
                                 select item).SingleOrDefaultAsync(ct) ?? throw MissingDelivery();
        var items = await db.FulfillmentExecutions.AsNoTracking().Where(x => x.OrganizationId == checkout.OrganizationId && x.EntitlementId == entitlement.Id && x.Status == "COMPLETED").OrderBy(x => x.CreatedAt).Select(x => new DeliveryItemResponse(x.Id, x.Type, x.Name, x.ExternalUrl, x.CompletedAt)).ToListAsync(ct);
        return new(entitlement.Id, entitlement.Status, items);
    }

    private static ExternalLinkFulfillmentResponse DefinitionResponse(FulfillmentDefinition x) => new(x.Id, x.Type, x.Name, x.ExternalUrl, x.Status);
    private static OrganizationException Missing() => new(404, "Entrega não encontrada.");
    private static OrganizationException MissingDelivery() => new(404, "Compra ou entrega não encontrada.");
}
