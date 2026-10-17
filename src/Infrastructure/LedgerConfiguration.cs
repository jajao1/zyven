using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zyven.Domain;
namespace Zyven.Infrastructure;

public sealed class LedgerAccountConfiguration : IEntityTypeConfiguration<LedgerAccount>
{
    public void Configure(EntityTypeBuilder<LedgerAccount> b)
    {
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasAlternateKey(x => new { x.Id, x.OrganizationId }); b.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique();
        b.Property(x => x.Code).HasMaxLength(40); b.Property(x => x.Name).HasMaxLength(100); b.Property(x => x.Type).HasMaxLength(20); b.Property(x => x.NormalSide).HasMaxLength(6); b.Property(x => x.Currency).HasMaxLength(3);
        b.ToTable(t => { t.HasCheckConstraint("CK_LedgerAccounts_Type", "\"Type\" IN ('ASSET','LIABILITY','REVENUE','EXPENSE','EQUITY')"); t.HasCheckConstraint("CK_LedgerAccounts_NormalSide", "\"NormalSide\" IN ('DEBIT','CREDIT')"); t.HasCheckConstraint("CK_LedgerAccounts_Currency", "\"Currency\" ~ '^[A-Z]{3}$'"); });
    }
}

public sealed class LedgerTransactionConfiguration : IEntityTypeConfiguration<LedgerTransaction>
{
    public void Configure(EntityTypeBuilder<LedgerTransaction> b)
    {
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Payment>().WithOne().HasForeignKey<LedgerTransaction>(x => new { x.PaymentId, x.OrganizationId }).HasPrincipalKey<Payment>(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        b.HasAlternateKey(x => new { x.Id, x.OrganizationId }); b.HasIndex(x => x.PaymentId).IsUnique(); b.HasIndex(x => new { x.OrganizationId, x.OccurredAt, x.Id });
        b.Property(x => x.Type).HasMaxLength(40); b.Property(x => x.Reference).HasMaxLength(100); b.Property(x => x.Currency).HasMaxLength(3);
        b.HasMany(x => x.Entries).WithOne().HasForeignKey(x => new { x.LedgerTransactionId, x.OrganizationId }).HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Entries).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> b)
    {
        b.HasOne<LedgerAccount>().WithMany().HasForeignKey(x => new { x.LedgerAccountId, x.OrganizationId }).HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Debit).HasPrecision(18, 2); b.Property(x => x.Credit).HasPrecision(18, 2); b.Property(x => x.Currency).HasMaxLength(3);
        b.HasIndex(x => new { x.OrganizationId, x.CreatedAt, x.Id }); b.HasIndex(x => x.LedgerAccountId);
        b.ToTable(t => { t.HasCheckConstraint("CK_LedgerEntries_OneSide", "(\"Debit\" > 0 AND \"Credit\" = 0) OR (\"Credit\" > 0 AND \"Debit\" = 0)"); t.HasCheckConstraint("CK_LedgerEntries_Currency", "\"Currency\" ~ '^[A-Z]{3}$'"); });
    }
}
