using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zyven.Domain;
namespace Zyven.Infrastructure;

public sealed class AutomationConfiguration : IEntityTypeConfiguration<Automation>
{
    public void Configure(EntityTypeBuilder<Automation> b) { b.HasAlternateKey(x => new { x.Id, x.OrganizationId }); b.HasIndex(x => new { x.OrganizationId, x.OfferId }).IsUnique(); b.HasOne<Offer>().WithMany().HasForeignKey(x => new { x.OfferId, x.OrganizationId }).HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Cascade); }
}
public sealed class AutomationStepConfiguration : IEntityTypeConfiguration<AutomationStep>
{
    public void Configure(EntityTypeBuilder<AutomationStep> b) { b.HasIndex(x => new { x.AutomationId, x.Version, x.Position }).IsUnique(); b.Property(x => x.Type).HasMaxLength(40); b.Property(x => x.ConfigurationJson).HasColumnType("jsonb"); b.HasOne<Automation>().WithMany().HasForeignKey(x => new { x.AutomationId, x.OrganizationId }).HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Cascade); b.ToTable(t => { t.HasCheckConstraint("CK_AutomationSteps_Position", "\"Position\" >= 0"); t.HasCheckConstraint("CK_AutomationSteps_Version", "\"Version\" >= 0"); }); }
}
public sealed class AutomationExecutionConfiguration : IEntityTypeConfiguration<AutomationExecution>
{
    public void Configure(EntityTypeBuilder<AutomationExecution> b) { b.HasIndex(x => new { x.EntitlementId, x.AutomationId, x.Version }).IsUnique(); b.HasIndex(x => new { x.Status, x.NextRunAt }); b.Property(x => x.Status).HasMaxLength(20); b.HasOne<Automation>().WithMany().HasForeignKey(x => new { x.AutomationId, x.OrganizationId }).HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict); b.HasOne<Entitlement>().WithMany().HasForeignKey(x => new { x.EntitlementId, x.OrganizationId }).HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict); }
}
