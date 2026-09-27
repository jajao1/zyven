using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zyven.Domain;
namespace Zyven.Infrastructure;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.HasAlternateKey(x => new { x.Id, x.OrganizationId });
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.Slug }).IsUnique();
        b.HasIndex(x => new { x.OrganizationId, x.CreatedAt, x.Id });
        b.Property(x => x.Name).HasMaxLength(200); b.Property(x => x.Slug).HasMaxLength(100);
        b.Property(x => x.Description).HasMaxLength(10000); b.Property(x => x.ImageUrl).HasMaxLength(2048);
        b.Property(x => x.Status).HasMaxLength(20);
        b.ToTable(t => t.HasCheckConstraint("CK_Products_Status", "\"Status\" IN ('DRAFT','ACTIVE','INACTIVE','ARCHIVED')"));
    }
}
public sealed class OfferConfiguration : IEntityTypeConfiguration<Offer>
{
    public void Configure(EntityTypeBuilder<Offer> b)
    {
        b.HasAlternateKey(x => new { x.Id, x.OrganizationId });
        b.HasOne<Product>().WithMany().HasForeignKey(x => new { x.ProductId, x.OrganizationId }).HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.Slug).IsUnique();
        b.HasIndex(x => new { x.OrganizationId, x.CreatedAt, x.Id });
        b.Property(x => x.Name).HasMaxLength(200); b.Property(x => x.Slug).HasMaxLength(100);
        b.Property(x => x.Headline).HasMaxLength(300); b.Property(x => x.Description).HasMaxLength(10000);
        b.Property(x => x.Price).HasPrecision(18, 2); b.Property(x => x.Currency).HasMaxLength(3);
        b.Property(x => x.Status).HasMaxLength(20); b.Property(x => x.BillingType).HasMaxLength(20);
        b.ToTable(t => { t.HasCheckConstraint("CK_Offers_Status", "\"Status\" IN ('DRAFT','ACTIVE','INACTIVE','ARCHIVED')"); t.HasCheckConstraint("CK_Offers_BillingType", "\"BillingType\" IN ('ONE_TIME','SUBSCRIPTION')"); t.HasCheckConstraint("CK_Offers_Price", "\"Price\" > 0"); t.HasCheckConstraint("CK_Offers_Currency", "\"Currency\" ~ '^[A-Z]{3}$'"); });
    }
}
