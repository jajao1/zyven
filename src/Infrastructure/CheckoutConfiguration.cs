using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zyven.Domain;
namespace Zyven.Infrastructure;

public sealed class OfferPageConfiguration : IEntityTypeConfiguration<OfferPage>
{
    public void Configure(EntityTypeBuilder<OfferPage> b)
    {
        b.HasKey(x => x.OfferId);
        b.HasOne<Offer>().WithMany().HasForeignKey(x => new { x.OfferId, x.OrganizationId }).HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.ContentJson).HasColumnType("jsonb");
        b.HasIndex(x => x.OrganizationId);
    }
}
public sealed class CheckoutConfiguration : IEntityTypeConfiguration<CheckoutSession>
{
    public void Configure(EntityTypeBuilder<CheckoutSession> b)
    {
        b.HasOne<Customer>().WithMany().HasForeignKey(x => new { x.CustomerId, x.OrganizationId }).HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Offer>().WithMany().HasForeignKey(x => new { x.OfferId, x.OrganizationId }).HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.AccessHash).HasMaxLength(64); b.Property(x => x.Status).HasMaxLength(20);
        b.Property(x => x.Name).HasMaxLength(200); b.Property(x => x.Email).HasMaxLength(254);
        b.Property(x => x.Phone).HasMaxLength(30); b.Property(x => x.Document).HasMaxLength(40);
        b.Property(x => x.FieldsJson).HasColumnType("jsonb"); b.Property(x => x.FormJson).HasColumnType("jsonb");
        b.Property(x => x.Price).HasPrecision(18, 2); b.Property(x => x.Currency).HasMaxLength(3);
        b.HasIndex(x => new { x.OrganizationId, x.CreatedAt }); b.HasIndex(x => new { x.Status, x.ExpiresAt });
        b.ToTable(t => { t.HasCheckConstraint("CK_Checkouts_Status", "\"Status\" IN ('CREATED','PENDING_PAYMENT','COMPLETED','EXPIRED','ABANDONED')"); t.HasCheckConstraint("CK_Checkouts_Price", "\"Price\" > 0"); });
    }
}
