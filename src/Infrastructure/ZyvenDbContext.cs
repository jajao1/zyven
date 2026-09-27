using Microsoft.EntityFrameworkCore;
using Zyven.Domain;
namespace Zyven.Infrastructure;

public sealed class ZyvenDbContext(DbContextOptions<ZyvenDbContext> options) : DbContext(options)
{
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();
    public DbSet<AuthEvent> AuthEvents => Set<AuthEvent>();
    public DbSet<User> Users => Set<User>();
    public DbSet<AuthSession> Sessions => Set<AuthSession>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AuditLog>().Property(x => x.Action).HasMaxLength(80);
        b.Entity<AuditLog>().HasIndex(x => new { x.OrganizationId, x.OccurredAt });
        b.Entity<Organization>().Property(x => x.Name).HasMaxLength(100);
        b.Entity<OrganizationMember>().HasIndex(x => new { x.OrganizationId, x.UserId }).IsUnique();
        b.Entity<OrganizationMember>().HasIndex(x => new { x.OrganizationId, x.Role });
        b.Entity<OrganizationMember>().Property(x => x.Role).HasMaxLength(20);
        b.Entity<OrganizationMember>().ToTable(t => t.HasCheckConstraint("CK_OrganizationMembers_Role", "\"Role\" IN ('OWNER', 'ADMIN', 'OPERATOR', 'FINANCE', 'SUPPORT')"));
        b.Entity<OrganizationMember>().HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<OrganizationMember>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
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
