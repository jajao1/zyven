using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Zyven.Application;
using Zyven.Domain;
namespace Zyven.Infrastructure;

public sealed class CatalogService(ZyvenDbContext db, TenantAuthorization tenants, TimeProvider time)
{
    public async Task<PageResponse<Product>> Products(Guid org, Guid user, int page, int size, CancellationToken ct)
    {
        await tenants.RequireMembership(org, user, ct); var offset = Offset(page, size);
        var query = db.Products.AsNoTracking().Where(x => x.OrganizationId == org);
        return new(await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip(offset).Take(size).ToListAsync(ct), page, size, await query.CountAsync(ct));
    }
    public async Task<PageResponse<Offer>> Offers(Guid org, Guid user, int page, int size, CancellationToken ct)
    {
        await tenants.RequireMembership(org, user, ct); var offset = Offset(page, size);
        var query = db.Offers.AsNoTracking().Where(x => x.OrganizationId == org);
        return new(await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip(offset).Take(size).ToListAsync(ct), page, size, await query.CountAsync(ct));
    }
    public async Task<Product> Product(Guid org, Guid id, Guid user, CancellationToken ct)
    {
        await tenants.RequireMembership(org, user, ct);
        return await db.Products.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == org && x.Id == id, ct) ?? throw Missing();
    }
    public async Task<Offer> Offer(Guid org, Guid id, Guid user, CancellationToken ct)
    {
        await tenants.RequireMembership(org, user, ct);
        return await db.Offers.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == org && x.Id == id, ct) ?? throw Missing();
    }
    public async Task<Product> SaveProduct(Guid org, Guid? id, Guid user, ProductRequest input, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await RequireWrite(org, user, ct);
        Validate(new ProductValidator(), input);
        var entity = id.HasValue ? await db.Products.SingleOrDefaultAsync(x => x.OrganizationId == org && x.Id == id, ct) ?? throw Missing() : new Product { OrganizationId = org, CreatedAt = time.GetUtcNow() };
        if (!id.HasValue && input.Status != "DRAFT") throw new OrganizationException(400, "Novos produtos começam como rascunho.");
        entity.Name = input.Name.Trim(); entity.Slug = input.Slug; entity.Description = input.Description?.Trim() ?? ""; entity.ImageUrl = string.IsNullOrEmpty(input.ImageUrl) ? null : input.ImageUrl; entity.Status = input.Status; entity.UpdatedAt = time.GetUtcNow();
        if (!id.HasValue) db.Products.Add(entity);
        Audit(org, user, entity.Id, id.HasValue ? "product.updated" : "product.created");
        await Save(ct); await transaction.CommitAsync(ct); return entity;
    }
    public async Task<Offer> SaveOffer(Guid org, Guid? id, Guid user, OfferRequest input, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await RequireWrite(org, user, ct);
        Validate(new OfferValidator(), input);
        var entity = id.HasValue ? await db.Offers.SingleOrDefaultAsync(x => x.OrganizationId == org && x.Id == id, ct) ?? throw Missing() : new Offer { OrganizationId = org, CreatedAt = time.GetUtcNow() };
        if (!await db.Products.AnyAsync(x => x.OrganizationId == org && x.Id == input.ProductId, ct)) throw Missing();
        if (!id.HasValue && input.Status != "DRAFT") throw new OrganizationException(400, "Novas ofertas começam como rascunho.");
        if (id.HasValue && entity.Price != input.Price) Audit(org, user, entity.Id, "offer.price_changed");
        entity.ProductId = input.ProductId; entity.Name = input.Name.Trim(); entity.Slug = input.Slug; entity.Headline = input.Headline?.Trim() ?? ""; entity.Description = input.Description?.Trim() ?? ""; entity.Price = input.Price; entity.Currency = input.Currency; entity.Status = input.Status; entity.BillingType = input.BillingType; entity.UpdatedAt = time.GetUtcNow();
        if (!id.HasValue) db.Offers.Add(entity);
        Audit(org, user, entity.Id, id.HasValue ? "offer.updated" : "offer.created");
        await Save(ct); await transaction.CommitAsync(ct); return entity;
    }
    private async Task RequireWrite(Guid org, Guid user, CancellationToken ct)
    {
        // The same organization lock serializes permission changes with catalog writes.
        if (!await db.Organizations.FromSqlInterpolated($"SELECT * FROM \"Organizations\" WHERE \"Id\" = {org} FOR UPDATE").AnyAsync(ct)) throw Missing();
        var membership = await tenants.RequireMembership(org, user, ct);
        if (membership.Role is not (OrganizationRoles.Owner or OrganizationRoles.Admin or OrganizationRoles.Operator)) throw new OrganizationException(403, "Seu papel não permite alterar o catálogo.");
    }
    private async Task Save(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { throw new OrganizationException(409, "Este slug já está em uso. Escolha outro."); }
    }
    private static void Validate<T>(IValidator<T> validator, T input)
    {
        var result = validator.Validate(input);
        if (!result.IsValid) throw new OrganizationException(400, string.Join(" ", result.Errors.Select(x => x.ErrorMessage)));
    }
    private void Audit(Guid org, Guid user, Guid target, string action) => db.AuditLogs.Add(new() { OrganizationId = org, ActorUserId = user, TargetId = target, Action = action, OccurredAt = time.GetUtcNow() });
    private static OrganizationException Missing() => new(404, "Item não encontrado.");
    private static int Offset(int page, int size)
    {
        if (page < 1 || size < 1 || size > 100 || (long)(page - 1) * size > int.MaxValue) throw new OrganizationException(400, "Paginação inválida.");
        return (page - 1) * size;
    }
}
