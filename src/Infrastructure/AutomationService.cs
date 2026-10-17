using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Zyven.Application;
using Zyven.Domain;
namespace Zyven.Infrastructure;

public sealed class AutomationService(ZyvenDbContext db, TenantAuthorization tenants, TimeProvider time)
{
    private static readonly Regex Variable = new(@"\{\{(buyer_name|buyer_email|offer_name|checkout_url)\}\}", RegexOptions.Compiled);
    private sealed record StepConfig(string? Text, string? Url, int? WaitMinutes, string? Condition);

    public async Task<AutomationResponse> Get(Guid org, Guid offer, Guid user, CancellationToken ct)
    {
        await tenants.RequireMembership(org, user, ct); await RequireOffer(org, offer, ct);
        var automation = await db.Automations.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == org && x.OfferId == offer, ct);
        if (automation is null) return new(Guid.Empty, 0, null, []);
        return await Response(automation, 0, ct);
    }

    public async Task<AutomationResponse> Save(Guid org, Guid offer, Guid user, AutomationDraftRequest request, CancellationToken ct)
    {
        await RequireWriter(org, user, ct); await RequireOffer(org, offer, ct); Validate(request.Steps);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var automation = await db.Automations.SingleOrDefaultAsync(x => x.OrganizationId == org && x.OfferId == offer, ct);
        var now = time.GetUtcNow();
        if (automation is null) { automation = new() { OrganizationId = org, OfferId = offer, CreatedAt = now, UpdatedAt = now }; db.Automations.Add(automation); }
        else { automation.UpdatedAt = now; await db.AutomationSteps.Where(x => x.AutomationId == automation.Id && x.Version == 0).ExecuteDeleteAsync(ct); }
        AddSteps(automation, org, 0, request.Steps, now); Audit(org, user, automation.Id, "automation.draft_saved");
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await Response(automation, 0, ct);
    }

    public async Task<AutomationResponse> Publish(Guid org, Guid offer, Guid user, CancellationToken ct)
    {
        await RequireWriter(org, user, ct); await RequireOffer(org, offer, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var automation = await db.Automations.FromSqlInterpolated($"SELECT * FROM \"Automations\" WHERE \"OrganizationId\" = {org} AND \"OfferId\" = {offer} FOR UPDATE").SingleOrDefaultAsync(ct) ?? throw new OrganizationException(400, "Salve o funil antes de publicar.");
        var draft = await db.AutomationSteps.Where(x => x.AutomationId == automation.Id && x.Version == 0).OrderBy(x => x.Position).ToListAsync(ct);
        var requests = draft.Select(ToRequest).ToList(); Validate(requests);
        var version = checked(automation.PublishedVersion + 1); var now = time.GetUtcNow(); AddSteps(automation, org, version, requests, now);
        automation.PublishedVersion = version; automation.PublishedAt = now; automation.UpdatedAt = now; Audit(org, user, automation.Id, "automation.published");
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await Response(automation, 0, ct);
    }

    private void AddSteps(Automation automation, Guid org, int version, IReadOnlyList<AutomationStepRequest> steps, DateTimeOffset now)
    {
        for (var i = 0; i < steps.Count; i++) db.AutomationSteps.Add(new() { OrganizationId = org, AutomationId = automation.Id, Version = version, Position = i, Type = steps[i].Type, ConfigurationJson = JsonSerializer.Serialize(new StepConfig(Clean(steps[i].Text), Clean(steps[i].Url), steps[i].WaitMinutes, Clean(steps[i].Condition))), CreatedAt = now });
    }
    private async Task<AutomationResponse> Response(Automation a, int version, CancellationToken ct)
    {
        var rows = await db.AutomationSteps.AsNoTracking().Where(x => x.AutomationId == a.Id && x.Version == version).OrderBy(x => x.Position).ToListAsync(ct);
        return new(a.Id, a.PublishedVersion, a.PublishedAt, rows.Select(x => { var c = JsonSerializer.Deserialize<StepConfig>(x.ConfigurationJson)!; return new AutomationStepResponse(x.Id, x.Position, x.Type, c.Text, c.Url, c.WaitMinutes, c.Condition); }).ToList());
    }
    private static AutomationStepRequest ToRequest(AutomationStep x) { var c = JsonSerializer.Deserialize<StepConfig>(x.ConfigurationJson)!; return new(x.Type, c.Text, c.Url, c.WaitMinutes, c.Condition); }
    private static void Validate(IReadOnlyList<AutomationStepRequest>? steps)
    {
        if (steps is null || steps.Count is < 1 or > 30) throw Bad("O funil deve ter de 1 a 30 etapas.");
        if (steps[^1].Type != "END") throw Bad("A última etapa deve encerrar o funil.");
        for (var i = 0; i < steps.Count; i++)
        {
            var s = steps[i]; if (!AutomationStepTypes.IsValid(s.Type)) throw Bad($"Tipo inválido na etapa {i + 1}.");
            if (s.Text is { Length: > 4000 } || s.Url is { Length: > 2048 }) throw Bad($"Conteúdo muito longo na etapa {i + 1}.");
            if (s.Text is not null && Regex.Replace(s.Text, Variable.ToString(), "").Contains("{{", StringComparison.Ordinal)) throw Bad($"Variável inválida na etapa {i + 1}.");
            if (s.Type == "SEND_TELEGRAM_MESSAGE" && string.IsNullOrWhiteSpace(s.Text)) throw Bad($"Informe a mensagem da etapa {i + 1}.");
            if (s.Type is "SEND_OFFER" or "CHECKOUT" && s.Url is not null && !SafeUrl(s.Url)) throw Bad($"Use uma URL HTTPS segura na etapa {i + 1}.");
            if (s.Type == "WAIT" && s.WaitMinutes is not (>= 1 and <= 43200)) throw Bad($"A espera da etapa {i + 1} deve ser de 1 minuto a 30 dias.");
            if (s.Type == "CONDITION" && s.Condition is not ("PAYMENT_CONFIRMED" or "ENTITLEMENT_ACTIVE")) throw Bad($"Condição inválida na etapa {i + 1}.");
        }
    }
    private async Task RequireWriter(Guid org, Guid user, CancellationToken ct) { var member = await tenants.RequireMembership(org, user, ct); if (member.Role is not (OrganizationRoles.Owner or OrganizationRoles.Admin or OrganizationRoles.Operator)) throw new OrganizationException(403, "Seu papel não permite esta alteração."); }
    private async Task RequireOffer(Guid org, Guid offer, CancellationToken ct) { if (!await db.Offers.AnyAsync(x => x.OrganizationId == org && x.Id == offer, ct)) throw new OrganizationException(404, "Oferta não encontrada."); }
    private void Audit(Guid org, Guid user, Guid target, string action) => db.AuditLogs.Add(new() { OrganizationId = org, ActorUserId = user, TargetId = target, Action = action, OccurredAt = time.GetUtcNow() });
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool SafeUrl(string value) => Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo);
    private static OrganizationException Bad(string message) => new(400, message);
}
