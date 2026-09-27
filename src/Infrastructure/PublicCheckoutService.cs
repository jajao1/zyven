using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Zyven.Application;
using Zyven.Domain;
namespace Zyven.Infrastructure;

public sealed class PublicCheckoutService(ZyvenDbContext db, TenantAuthorization tenants, CustomerService customers, TimeProvider time)
{
    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value);
    private static T Deserialize<T>(string value) => JsonSerializer.Deserialize<T>(value)!;
    public async Task<PageContent> Page(Guid org, Guid offer, Guid user, CancellationToken ct)
    {
        await tenants.RequireMembership(org, user, ct);
        var item = await db.Offers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == offer && x.OrganizationId == org, ct) ?? throw Missing();
        return await Content(item, ct);
    }
    public async Task<PageContent> SavePage(Guid org, Guid offer, Guid user, PageContent input, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (!await db.Organizations.FromSqlInterpolated($"SELECT * FROM \"Organizations\" WHERE \"Id\" = {org} FOR UPDATE").AnyAsync(ct)) throw Missing();
        var member = await tenants.RequireMembership(org, user, ct);
        if (member.Role is not (OrganizationRoles.Owner or OrganizationRoles.Admin or OrganizationRoles.Operator)) throw new OrganizationException(403, "Seu papel não permite editar a página.");
        if (!await db.Offers.AnyAsync(x => x.Id == offer && x.OrganizationId == org, ct)) throw Missing();
        var validation = new PageContentValidator().Validate(input);
        if (!validation.IsValid) throw new OrganizationException(400, "Revise os blocos da página e as URLs HTTPS.");
        var page = await db.OfferPages.SingleOrDefaultAsync(x => x.OfferId == offer && x.OrganizationId == org, ct);
        if (page is null) { page = new() { OfferId = offer, OrganizationId = org }; db.OfferPages.Add(page); }
        page.ContentJson = Serialize(input); page.UpdatedAt = time.GetUtcNow();
        db.AuditLogs.Add(new() { OrganizationId = org, ActorUserId = user, TargetId = offer, Action = "offer.page_updated", OccurredAt = time.GetUtcNow() });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return input;
    }
    private async Task<PageContent> Content(Offer offer, CancellationToken ct)
    {
        var page = await db.OfferPages.AsNoTracking().SingleOrDefaultAsync(x => x.OfferId == offer.Id && x.OrganizationId == offer.OrganizationId, ct);
        return page is null ? new() { Title = string.IsNullOrWhiteSpace(offer.Headline) ? offer.Name : offer.Headline, Description = offer.Description } : Deserialize<PageContent>(page.ContentJson);
    }
    private async Task<Offer> Active(string slug, CancellationToken ct) => await db.Offers.AsNoTracking().Where(x => x.Slug == slug && x.Status == "ACTIVE" && db.Products.Any(p => p.Id == x.ProductId && p.OrganizationId == x.OrganizationId && p.Status == "ACTIVE")).SingleOrDefaultAsync(ct) ?? throw Missing();
    public async Task<PublicOffer> Public(string slug, CancellationToken ct)
    {
        var offer = await Active(slug, ct);
        var product = await db.Products.AsNoTracking().SingleAsync(x => x.Id == offer.ProductId && x.OrganizationId == offer.OrganizationId, ct);
        return new(offer.Slug, offer.Name, product.Name, Money(offer.Price), offer.Currency, offer.BillingType, await Content(offer, ct));
    }
    public async Task<(CheckoutResponse Response, string Secret)> Create(string slug, CheckoutInput input, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var offer = await Active(slug, ct);
        // Serialize publication/price changes with the checkout's authoritative snapshot.
        await db.Organizations.FromSqlInterpolated($"SELECT * FROM \"Organizations\" WHERE \"Id\" = {offer.OrganizationId} FOR UPDATE").SingleAsync(ct);
        offer = await Active(slug, ct);
        var page = await Content(offer, ct); ValidateInput(input, page.Fields);
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(48));
        var session = new CheckoutSession { OrganizationId = offer.OrganizationId, OfferId = offer.Id, AccessHash = Hash(secret), Price = offer.Price, Currency = offer.Currency, CreatedAt = time.GetUtcNow(), ExpiresAt = time.GetUtcNow().AddMinutes(30), FormJson = Serialize(page.Fields) };
        session.CustomerId = await customers.Resolve(session.OrganizationId, input, ct);
        SetInput(session, input); db.Checkouts.Add(session); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return (Response(session, offer.Slug), secret);
    }
    public async Task<CheckoutResponse> Read(Guid id, string? secret, CancellationToken ct)
    {
        var session = await Authorized(id, secret, ct);
        return Response(session, await Slug(session, ct));
    }
    private Task<string> Slug(CheckoutSession session, CancellationToken ct) => db.Offers.Where(x => x.Id == session.OfferId && x.OrganizationId == session.OrganizationId).Select(x => x.Slug).SingleAsync(ct);
    public async Task<CheckoutResponse> Update(Guid id, string? secret, CheckoutInput input, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var initial = await Authorized(id, secret, ct);
        await db.Organizations.FromSqlInterpolated($"SELECT * FROM \"Organizations\" WHERE \"Id\" = {initial.OrganizationId} FOR UPDATE").SingleAsync(ct);
        // Re-read under the lock: waiting cannot extend expiry or reuse stale checkout data.
        await db.Entry(initial).ReloadAsync(ct);
        var session = await Authorized(id, secret, ct);
        ValidateInput(input, Deserialize<CheckoutField[]>(session.FormJson));
        session.CustomerId = await customers.Resolve(session.OrganizationId, input, ct);
        SetInput(session, input); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return Response(session, await Slug(session, ct));
    }
    private async Task<CheckoutSession> Authorized(Guid id, string? secret, CancellationToken ct)
    {
        if (secret is null || secret.Length != 96) throw Missing();
        var hash = Hash(secret);
        var session = await db.Checkouts.SingleOrDefaultAsync(x => x.Id == id && x.AccessHash == hash, ct) ?? throw Missing();
        if (session.ExpiresAt <= time.GetUtcNow() || session.Status != "CREATED") throw new OrganizationException(410, "Este checkout expirou ou não está disponível. Comece novamente pela oferta.");
        if (!await db.Offers.AnyAsync(x => x.Id == session.OfferId && x.OrganizationId == session.OrganizationId && x.Status == "ACTIVE" && db.Products.Any(p => p.Id == x.ProductId && p.OrganizationId == x.OrganizationId && p.Status == "ACTIVE"), ct)) throw Missing();
        return session;
    }
    private static void ValidateInput(CheckoutInput input, CheckoutField[] fields)
    {
        if (!new CheckoutInputValidator().Validate(input).IsValid) throw new OrganizationException(400, "Revise nome, email e dados de contato.");
        var values = input.Fields ?? [];
        if (values.Keys.Any(key => !fields.Any(f => f.Key == key)) || fields.Any(f => f.Required && (!values.TryGetValue(f.Key, out var value) || string.IsNullOrWhiteSpace(value)))) throw new OrganizationException(400, "Preencha os campos adicionais obrigatórios.");
    }
    private static void SetInput(CheckoutSession session, CheckoutInput input) { session.Name = input.Name.Trim(); session.Email = CustomerIdentity.TrimEmail(input.Email); session.Phone = input.Phone?.Trim(); session.Document = input.Document?.Trim(); session.FieldsJson = Serialize(input.Fields ?? []); }
    private static CheckoutResponse Response(CheckoutSession session, string slug) => new(session.Id, session.Status, Money(session.Price), session.Currency, session.ExpiresAt, session.Name, session.Email, session.Phone, session.Document, Deserialize<Dictionary<string, string>>(session.FieldsJson), slug);
    private static string Money(decimal price) => price.ToString("0.00", CultureInfo.InvariantCulture);
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static OrganizationException Missing() => new(404, "Oferta ou checkout não encontrado.");
}
