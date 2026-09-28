using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zyven.Domain;
namespace Zyven.Infrastructure;

public sealed class FulfillmentDefinitionConfiguration : IEntityTypeConfiguration<FulfillmentDefinition>
{
    public void Configure(EntityTypeBuilder<FulfillmentDefinition> b)
    {
        b.HasAlternateKey(x => new { x.Id, x.OrganizationId });
        b.HasOne<Offer>().WithMany().HasForeignKey(x => new { x.OfferId, x.OrganizationId }).HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Type).HasMaxLength(30); b.Property(x => x.Name).HasMaxLength(100); b.Property(x => x.ExternalUrl).HasMaxLength(2048); b.Property(x => x.Status).HasMaxLength(20);
        b.HasIndex(x => new { x.OrganizationId, x.OfferId, x.Type }).IsUnique();
        b.ToTable(t => { t.HasCheckConstraint("CK_FulfillmentDefinitions_Type", "\"Type\" = 'EXTERNAL_LINK'"); t.HasCheckConstraint("CK_FulfillmentDefinitions_Status", "\"Status\" IN ('ACTIVE','INACTIVE')"); });
    }
}

public sealed class EntitlementConfiguration : IEntityTypeConfiguration<Entitlement>
{
    public void Configure(EntityTypeBuilder<Entitlement> b)
    {
        b.HasAlternateKey(x => new { x.Id, x.OrganizationId });
        b.HasOne<Payment>().WithOne().HasForeignKey<Entitlement>(x => new { x.PaymentId, x.OrganizationId }).HasPrincipalKey<Payment>(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Customer>().WithMany().HasForeignKey(x => new { x.CustomerId, x.OrganizationId }).HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Offer>().WithMany().HasForeignKey(x => new { x.OfferId, x.OrganizationId }).HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Type).HasMaxLength(30); b.Property(x => x.Status).HasMaxLength(20);
        b.HasIndex(x => x.PaymentId).IsUnique(); b.HasIndex(x => new { x.OrganizationId, x.CustomerId, x.CreatedAt });
        b.ToTable(t => { t.HasCheckConstraint("CK_Entitlements_Status", "\"Status\" IN ('PENDING','ACTIVE','EXPIRED','REVOKED','FAILED')"); t.HasCheckConstraint("CK_Entitlements_Type", "\"Type\" = 'PURCHASE'"); });
    }
}

public sealed class FulfillmentExecutionConfiguration : IEntityTypeConfiguration<FulfillmentExecution>
{
    public void Configure(EntityTypeBuilder<FulfillmentExecution> b)
    {
        b.HasOne<Entitlement>().WithMany().HasForeignKey(x => new { x.EntitlementId, x.OrganizationId }).HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FulfillmentDefinition>().WithMany().HasForeignKey(x => new { x.FulfillmentDefinitionId, x.OrganizationId }).HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Type).HasMaxLength(30); b.Property(x => x.Status).HasMaxLength(20); b.Property(x => x.Name).HasMaxLength(100); b.Property(x => x.ExternalUrl).HasMaxLength(2048);
        b.HasIndex(x => new { x.EntitlementId, x.FulfillmentDefinitionId }).IsUnique(); b.HasIndex(x => new { x.OrganizationId, x.CreatedAt });
        b.ToTable(t => { t.HasCheckConstraint("CK_FulfillmentExecutions_Status", "\"Status\" IN ('PENDING','PROCESSING','COMPLETED','FAILED')"); t.HasCheckConstraint("CK_FulfillmentExecutions_Type", "\"Type\" = 'EXTERNAL_LINK'"); });
    }
}
