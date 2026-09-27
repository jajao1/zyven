using Microsoft.EntityFrameworkCore;
using Zyven.Application;
using Zyven.Domain;
namespace Zyven.Infrastructure;

public sealed record CustomerResponse(Guid Id, string Name, string Email, string? Phone, string? Document, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed class CustomerService(ZyvenDbContext db, TenantAuthorization tenants, TimeProvider time)
{
    // Caller holds the organization row lock and commits the checkout and customer together.
    internal async Task<Guid> Resolve(Guid org, CheckoutInput input, CancellationToken ct)
    {
        var email = CustomerIdentity.Email(input.Email); var phone = CustomerIdentity.Phone(input.Phone);
        var byEmail = await db.Customers.SingleOrDefaultAsync(x => x.OrganizationId == org && x.NormalizedEmail == email, ct);
        var phoneMatches = phone is null ? [] : await db.Customers.Where(x => x.OrganizationId == org && x.NormalizedPhone == phone).Select(x => x.Id).ToListAsync(ct);
        // A claimed phone cannot join separate email identities. Legacy duplicate phones stay distinct.
        if (phoneMatches.Any(id => byEmail is null || id != byEmail.Id)) throw new OrganizationException(409, "Não foi possível associar os dados de contato. Revise os dados ou entre em contato com o vendedor.");
        if (byEmail is not null) return byEmail.Id;
        var customer = new Customer { OrganizationId = org, Name = input.Name.Trim(), Email = CustomerIdentity.TrimEmail(input.Email), NormalizedEmail = email, Phone = phone, NormalizedPhone = phone, Document = input.Document?.Trim(), CreatedAt = time.GetUtcNow(), UpdatedAt = time.GetUtcNow() };
        db.Customers.Add(customer); return customer.Id;
    }
    public async Task<PageResponse<CustomerResponse>> List(Guid org, Guid user, int page, int size, CancellationToken ct)
    {
        await tenants.RequireMembership(org, user, ct);
        var offset = ((long)page - 1) * size;
        if (page < 1 || size is < 1 or > 100 || offset > int.MaxValue) throw new OrganizationException(400, "Paginação inválida.");
        var query = db.Customers.AsNoTracking().Where(x => x.OrganizationId == org);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((int)offset).Take(size).Select(x => new CustomerResponse(x.Id, x.Name, x.Email, x.Phone, x.Document, x.CreatedAt, x.UpdatedAt)).ToListAsync(ct);
        return new(items, page, size, await query.CountAsync(ct));
    }
    public async Task<CustomerResponse> Detail(Guid org, Guid id, Guid user, CancellationToken ct)
    {
        await tenants.RequireMembership(org, user, ct);
        return await db.Customers.AsNoTracking().Where(x => x.OrganizationId == org && x.Id == id).Select(x => new CustomerResponse(x.Id, x.Name, x.Email, x.Phone, x.Document, x.CreatedAt, x.UpdatedAt)).SingleOrDefaultAsync(ct) ?? throw new OrganizationException(404, "Cliente não encontrado.");
    }
}
