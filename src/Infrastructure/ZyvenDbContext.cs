using Microsoft.EntityFrameworkCore;
using Zyven.Domain;
namespace Zyven.Infrastructure;

public sealed class ZyvenDbContext(DbContextOptions<ZyvenDbContext> options) : DbContext(options)
{
    public DbSet<MerchantAccount> MerchantAccounts => Set<MerchantAccount>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentWebhookEvent> PaymentWebhookEvents => Set<PaymentWebhookEvent>();
    public DbSet<LedgerAccount> LedgerAccounts => Set<LedgerAccount>();
    public DbSet<LedgerTransaction> LedgerTransactions => Set<LedgerTransaction>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<OfferPage> OfferPages => Set<OfferPage>();
    public DbSet<CheckoutSession> Checkouts => Set<CheckoutSession>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Offer> Offers => Set<Offer>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();
    public DbSet<AuthEvent> AuthEvents => Set<AuthEvent>();
    public DbSet<User> Users => Set<User>();
    public DbSet<AuthSession> Sessions => Set<AuthSession>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.ApplyConfiguration(new MerchantAccountConfiguration());
        b.ApplyConfiguration(new PaymentConfiguration());
        b.ApplyConfiguration(new PaymentWebhookEventConfiguration());
        b.ApplyConfiguration(new LedgerAccountConfiguration()); b.ApplyConfiguration(new LedgerTransactionConfiguration()); b.ApplyConfiguration(new LedgerEntryConfiguration());
        b.ApplyConfiguration(new CustomerConfiguration());
        b.ApplyConfiguration(new OfferPageConfiguration());
        b.ApplyConfiguration(new CheckoutConfiguration());
        b.ApplyConfiguration(new ProductConfiguration());
        b.ApplyConfiguration(new OfferConfiguration());
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
