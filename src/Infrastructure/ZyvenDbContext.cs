using Microsoft.EntityFrameworkCore;
using Zyven.Domain;
namespace Zyven.Infrastructure;

public sealed class ZyvenDbContext(DbContextOptions<ZyvenDbContext> options) : DbContext(options)
{
    public DbSet<AuthEvent> AuthEvents => Set<AuthEvent>();
    public DbSet<User> Users => Set<User>();
    public DbSet<AuthSession> Sessions => Set<AuthSession>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AuthEvent>().Property(x => x.Action).HasMaxLength(80);
        b.Entity<AuthEvent>().HasIndex(x => new { x.UserId, x.OccurredAt });
        b.Entity<User>().HasIndex(x => x.NormalizedEmail).IsUnique();
        b.Entity<User>().Property(x => x.Email).HasMaxLength(254); b.Entity<User>().Property(x => x.NormalizedEmail).HasMaxLength(254); b.Entity<User>().Property(x => x.DisplayName).HasMaxLength(100);
        b.Entity<AuthSession>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<AuthSession>().HasIndex(x => x.ExpiresAt);
        b.Entity<RefreshToken>().HasKey(x => x.Hash); b.Entity<RefreshToken>().Property(x => x.Hash).HasMaxLength(64);
        b.Entity<RefreshToken>().HasOne(x => x.Session).WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
    }
}


