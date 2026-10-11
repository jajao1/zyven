using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zyven.Domain;
namespace Zyven.Infrastructure;

public sealed class TelegramIntegrationConfiguration : IEntityTypeConfiguration<TelegramIntegration> { public void Configure(EntityTypeBuilder<TelegramIntegration> b) { b.HasIndex(x => x.OrganizationId).IsUnique(); b.Property(x => x.BotName).HasMaxLength(100); b.Property(x => x.BotUsername).HasMaxLength(100); b.Property(x => x.TokenFingerprint).HasMaxLength(12); b.Property(x => x.Status).HasMaxLength(20); b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict); } }
public sealed class TelegramLinkTokenConfiguration : IEntityTypeConfiguration<TelegramLinkToken> { public void Configure(EntityTypeBuilder<TelegramLinkToken> b) { b.HasIndex(x => x.Hash).IsUnique(); b.Property(x => x.Hash).HasMaxLength(64); b.HasIndex(x => x.ExpiresAt); b.HasOne<FulfillmentExecution>().WithMany().HasForeignKey(x => x.FulfillmentExecutionId).OnDelete(DeleteBehavior.Cascade); } }
public sealed class TelegramAccessGrantConfiguration : IEntityTypeConfiguration<TelegramAccessGrant> { public void Configure(EntityTypeBuilder<TelegramAccessGrant> b) { b.HasIndex(x => x.FulfillmentExecutionId).IsUnique(); b.HasIndex(x => new { x.OrganizationId, x.TelegramUserId }); b.Property(x => x.Status).HasMaxLength(20); b.HasOne<FulfillmentExecution>().WithMany().HasForeignKey(x => x.FulfillmentExecutionId).OnDelete(DeleteBehavior.Restrict); } }
