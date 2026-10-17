using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zyven.Domain;
namespace Zyven.Infrastructure;

public sealed class BuyerAccessCodeConfiguration : IEntityTypeConfiguration<BuyerAccessCode>
{
    public void Configure(EntityTypeBuilder<BuyerAccessCode> b)
    {
        b.Property(x => x.NormalizedEmail).HasMaxLength(254);
        b.Property(x => x.CodeHash).HasMaxLength(64);
        b.HasIndex(x => new { x.NormalizedEmail, x.CreatedAt });
        b.HasIndex(x => x.ExpiresAt);
    }
}

public sealed class BuyerSessionConfiguration : IEntityTypeConfiguration<BuyerSession>
{
    public void Configure(EntityTypeBuilder<BuyerSession> b)
    {
        b.Property(x => x.NormalizedEmail).HasMaxLength(254);
        b.Property(x => x.TokenHash).HasMaxLength(64);
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => new { x.NormalizedEmail, x.ExpiresAt });
    }
}
