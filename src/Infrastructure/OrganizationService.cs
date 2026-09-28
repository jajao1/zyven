using Microsoft.EntityFrameworkCore;
using Zyven.Application;
using Zyven.Domain;
namespace Zyven.Infrastructure;

// Membership is always loaded from the database, never from client-supplied role claims.
public sealed class TenantAuthorization(ZyvenDbContext db)
{
    public async Task<OrganizationMember> RequireMembership(Guid organizationId, Guid userId, CancellationToken ct) =>
        await db.OrganizationMembers.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.UserId == userId, ct)
        ?? throw new OrganizationException(404, "Organização não encontrada.");
}

public sealed class OrganizationService(ZyvenDbContext db, TenantAuthorization tenants, TimeProvider time)
{
    public async Task<PageResponse<OrganizationResponse>> List(Guid userId, int page, int pageSize, CancellationToken ct)
    {
        var offset = Offset(page, pageSize);
        var query = db.OrganizationMembers.AsNoTracking().Where(x => x.UserId == userId);
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.Organization.Name).ThenBy(x => x.OrganizationId).Skip(offset).Take(pageSize)
            .Select(x => new OrganizationResponse(x.OrganizationId, x.Organization.Name, x.Organization.CreatedAt, x.Organization.UpdatedAt, x.Role)).ToListAsync(ct);
        return new(items, page, pageSize, total);
    }
    public async Task<OrganizationResponse> Get(Guid id, Guid userId, CancellationToken ct)
    {
        var member = await tenants.RequireMembership(id, userId, ct);
        var org = await db.Organizations.AsNoTracking().SingleAsync(x => x.Id == id, ct);
        return Response(org, member.Role);
    }

    public async Task<OrganizationResponse> Create(Guid userId, OrganizationRequest request, CancellationToken ct)
    {
        var name = Name(request.Name);
        var now = time.GetUtcNow();
        var org = new Organization { Name = name, CreatedAt = now, UpdatedAt = now };
        db.Organizations.Add(org);
        db.MerchantAccounts.Add(new() { OrganizationId = org.Id, CreatedAt = now, UpdatedAt = now });
        db.OrganizationMembers.Add(new() { Organization = org, UserId = userId, Role = OrganizationRoles.Owner, CreatedAt = now });
        Audit(org.Id, userId, org.Id, "organization.created");
        // SaveChanges wraps the organization, pending merchant, owner membership and audit in one transaction.
        await db.SaveChangesAsync(ct);
        return Response(org, OrganizationRoles.Owner);
    }

    public async Task<OrganizationResponse> Rename(Guid id, Guid userId, OrganizationRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var org = await Lock(id, ct);
        var actor = await tenants.RequireMembership(id, userId, ct);
        if (!OrganizationRoles.CanRename(actor.Role)) throw Forbidden();
        org.Name = Name(request.Name); org.UpdatedAt = time.GetUtcNow();
        Audit(id, userId, id, "organization.renamed");
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return Response(org, actor.Role);
    }

    public async Task<PaymentAccountResponse> ConnectPaymentAccount(Guid id, Guid userId, PaymentAccountRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await Lock(id, ct);
        var actor = await tenants.RequireMembership(id, userId, ct);
        if (actor.Role is not (OrganizationRoles.Owner or OrganizationRoles.Admin)) throw Forbidden();
        var merchant = await db.MerchantAccounts.SingleAsync(x => x.OrganizationId == id, ct);
        try { merchant.Activate(request.ProviderRecipientId ?? "", time.GetUtcNow()); }
        catch (ArgumentException) { throw new OrganizationException(400, "Informe o identificador de recebedor da SyncPay."); }
        Audit(id, userId, merchant.Id, "payment_account.connected");
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { throw new OrganizationException(409, "Esta conta SyncPay já está vinculada a outra organização."); }
        await transaction.CommitAsync(ct);
        return new(merchant.Status, merchant.ProviderRecipientId);
    }

    public async Task<PageResponse<MemberResponse>> Members(Guid id, Guid userId, int page, int pageSize, CancellationToken ct)
    {
        await tenants.RequireMembership(id, userId, ct);
        var offset = Offset(page, pageSize);
        var query = db.OrganizationMembers.AsNoTracking().Where(x => x.OrganizationId == id);
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Skip(offset).Take(pageSize)
            .Select(x => new MemberResponse(x.Id, x.UserId, x.User.Email, x.User.DisplayName, x.Role, x.CreatedAt)).ToListAsync(ct);
        return new(items, page, pageSize, total);
    }
    public async Task<MemberResponse> Add(Guid id, Guid userId, AddMemberRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await Lock(id, ct);
        var actor = await tenants.RequireMembership(id, userId, ct);
        var role = Role(request.Role);
        if (!OrganizationRoles.CanManage(actor.Role, role)) throw Forbidden();
        if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 254) throw new OrganizationException(400, "Informe um e-mail válido.");
        var email = AuthService.NormalizeEmail(request.Email);
        var user = await db.Users.SingleOrDefaultAsync(x => x.NormalizedEmail == email, ct)
            ?? throw new OrganizationException(404, "Não encontramos uma conta com este e-mail. Peça à pessoa que se cadastre primeiro.");
        if (await db.OrganizationMembers.AnyAsync(x => x.OrganizationId == id && x.UserId == user.Id, ct))
            throw new OrganizationException(409, "Esta pessoa já participa da organização.");
        var member = new OrganizationMember { OrganizationId = id, User = user, Role = role, CreatedAt = time.GetUtcNow() };
        db.OrganizationMembers.Add(member);
        Audit(id, userId, member.Id, "organization.member.added");
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return Response(member);
    }

    public async Task<MemberResponse?> Change(Guid id, Guid memberId, Guid userId, ChangeRoleRequest? request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Serialize membership mutations before reading permissions and counting owners.
        await Lock(id, ct);
        var actor = await tenants.RequireMembership(id, userId, ct);
        var member = await db.OrganizationMembers.Include(x => x.User).SingleOrDefaultAsync(x => x.OrganizationId == id && x.Id == memberId, ct)
            ?? throw new OrganizationException(404, "Membro não encontrado.");
        var nextRole = request is null ? null : Role(request.Role);
        if (!OrganizationRoles.CanManage(actor.Role, member.Role) || nextRole is not null && !OrganizationRoles.CanManage(actor.Role, nextRole)) throw Forbidden();
        if (member.Role == OrganizationRoles.Owner && nextRole != OrganizationRoles.Owner &&
            await db.OrganizationMembers.CountAsync(x => x.OrganizationId == id && x.Role == OrganizationRoles.Owner, ct) <= 1)
            throw new OrganizationException(409, "A organização precisa de pelo menos um proprietário.");
        if (nextRole is null) db.OrganizationMembers.Remove(member); else member.Role = nextRole;
        Audit(id, userId, member.Id, nextRole is null ? "organization.member.removed" : "organization.member.role_changed");
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return nextRole is null ? null : Response(member);
    }

    private void Audit(Guid organizationId, Guid actor, Guid target, string action) =>
        db.AuditLogs.Add(new() { OrganizationId = organizationId, ActorUserId = actor, TargetId = target, Action = action, OccurredAt = time.GetUtcNow() });
    private static int Offset(int page, int pageSize)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            throw new OrganizationException(400, "Paginação inválida. Use páginas positivas e de 1 a 100 itens.");
        return (page - 1) * pageSize;
    }
    private async Task<Organization> Lock(Guid id, CancellationToken ct) =>
        await db.Organizations.FromSqlInterpolated($"SELECT * FROM \"Organizations\" WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct)
        ?? throw new OrganizationException(404, "Organização não encontrada.");
    private static string Name(string? name) => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 100 ? name.Trim() : throw new OrganizationException(400, "Use um nome de 1 a 100 caracteres.");
    private static string Role(string? role) => OrganizationRoles.IsValid(role) ? role! : throw new OrganizationException(400, "Papel inválido.");
    private static OrganizationException Forbidden() => new(403, "Seu papel não permite esta alteração.");
    private static OrganizationResponse Response(Organization org, string role) => new(org.Id, org.Name, org.CreatedAt, org.UpdatedAt, role);
    private static MemberResponse Response(OrganizationMember member) => new(member.Id, member.UserId, member.User.Email, member.User.DisplayName, member.Role, member.CreatedAt);
}
