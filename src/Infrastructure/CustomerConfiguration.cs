using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zyven.Domain;
namespace Zyven.Infrastructure;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.HasAlternateKey(x => new { x.Id, x.OrganizationId });
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Email).HasMaxLength(254); b.Property(x => x.NormalizedEmail).HasMaxLength(254);
        b.Property(x => x.Phone).HasMaxLength(30); b.Property(x => x.NormalizedPhone).HasMaxLength(16);
        b.Property(x => x.Document).HasMaxLength(40);
        b.HasIndex(x => new { x.OrganizationId, x.NormalizedEmail }).IsUnique();
        b.HasIndex(x => new { x.OrganizationId, x.NormalizedPhone });
        b.HasIndex(x => new { x.OrganizationId, x.CreatedAt });
    }
}
