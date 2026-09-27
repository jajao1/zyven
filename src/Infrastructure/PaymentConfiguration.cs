using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zyven.Domain;
namespace Zyven.Infrastructure;

public sealed class MerchantAccountConfiguration : IEntityTypeConfiguration<MerchantAccount>
{
    public void Configure(EntityTypeBuilder<MerchantAccount> b)
    {
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.OrganizationId).IsUnique();
        b.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("PENDING");
        b.ToTable(t => t.HasCheckConstraint("CK_MerchantAccounts_Status", "\"Status\" IN ('PENDING','ACTIVE','SUSPENDED','BLOCKED')"));
    }
}
public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.HasOne<MerchantAccount>().WithMany().HasForeignKey(x => new { x.MerchantAccountId, x.OrganizationId }).HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Customer>().WithMany().HasForeignKey(x => new { x.CustomerId, x.OrganizationId }).HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CheckoutSession>().WithMany().HasForeignKey(x => new { x.CheckoutSessionId, x.OrganizationId }).HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Offer>().WithMany().HasForeignKey(x => new { x.OfferId, x.OrganizationId }).HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        // Migration also adds FK_Payments_CheckoutSnapshot across checkout/customer/offer/org.
        // Keeping that key database-only allows EF to update CustomerId before payment creation.
        b.Property(x => x.GrossAmount).HasPrecision(18, 2); b.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        b.Property(x => x.OrderBumpAmount).HasPrecision(18, 2); b.Property(x => x.PlatformFee).HasPrecision(18, 2); b.Property(x => x.NetAmount).HasPrecision(18, 2);
        b.Property(x => x.Currency).HasMaxLength(3); b.Property(x => x.PaymentMethod).HasMaxLength(10); b.Property(x => x.Status).HasMaxLength(20);
        b.Property(x => x.Provider).HasMaxLength(100); b.Property(x => x.ProviderTransactionId).HasMaxLength(200); b.Property(x => x.ExternalReference).HasMaxLength(100); b.Property(x => x.EndToEndId).HasMaxLength(200);
        b.HasIndex(x => new { x.MerchantAccountId, x.ExternalReference }).IsUnique();
        b.HasIndex(x => new { x.MerchantAccountId, x.Provider, x.ProviderTransactionId }).IsUnique().HasFilter("\"ProviderTransactionId\" IS NOT NULL");
        b.HasIndex(x => new { x.OrganizationId, x.CreatedAt }); b.HasIndex(x => new { x.Status, x.ExpiresAt });
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Payments_Status", "\"Status\" IN ('PENDING','PROCESSING','PAID','EXPIRED','FAILED','CANCELLED','REFUNDED','CHARGEBACK')");
            t.HasCheckConstraint("CK_Payments_Method", "\"PaymentMethod\" IN ('PIX','CARD')");
            t.HasCheckConstraint("CK_Payments_Amounts", "\"GrossAmount\" > 0 AND \"DiscountAmount\" >= 0 AND \"OrderBumpAmount\" >= 0 AND \"PlatformFee\" >= 0 AND \"NetAmount\" >= 0 AND \"NetAmount\" = \"GrossAmount\" - \"PlatformFee\"");
            t.HasCheckConstraint("CK_Payments_Currency", "\"Currency\" ~ '^[A-Z]{3}$'");
            t.HasCheckConstraint("CK_Payments_ProviderReference", "(\"ProviderTransactionId\" IS NULL OR (length(btrim(\"ProviderTransactionId\")) > 0 AND \"Provider\" IS NOT NULL AND length(btrim(\"Provider\")) > 0)) AND length(btrim(\"ExternalReference\")) > 0");
            t.HasCheckConstraint("CK_Payments_Expiry", "\"ExpiresAt\" > \"CreatedAt\"");
        });
    }
}
