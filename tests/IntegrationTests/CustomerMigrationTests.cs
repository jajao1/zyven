using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Zyven.Domain;
using Zyven.Infrastructure;
namespace IntegrationTests;

public class CustomerMigrationTests
{
    [Fact]
    public async Task Upgrade_backfills_oldest_profile_preserves_snapshots_and_enforces_tenant_foreign_key()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Database") ?? throw new InvalidOperationException("Test database required.");
        var databaseName = "zyven_upgrade_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connection) { Database = databaseName, Pooling = false };
            await using var db = new ZyvenDbContext(new DbContextOptionsBuilder<ZyvenDbContext>().UseNpgsql(builder.ConnectionString).Options);
            await db.GetService<IMigrator>().MigrateAsync("20260927182611_OfferPagesAndCheckouts");
            var org = new Organization { Name = "Legacy" }; var other = new Organization { Name = "Other" }; db.Organizations.AddRange(org, other); await db.SaveChangesAsync();
            var product = new Product { OrganizationId = org.Id, Name = "Old", Slug = "old" }; var product2 = new Product { OrganizationId = other.Id, Name = "Other", Slug = "old" }; db.Products.AddRange(product, product2); await db.SaveChangesAsync();
            var offer = new Offer { OrganizationId = org.Id, ProductId = product.Id, Name = "Old", Slug = "old", Price = 10 }; var offer2 = new Offer { OrganizationId = other.Id, ProductId = product2.Id, Name = "Other", Slug = "other", Price = 10 }; db.Offers.AddRange(offer, offer2); await db.SaveChangesAsync();
            var first = Guid.NewGuid(); var second = Guid.NewGuid(); var third = Guid.NewGuid(); var foreign = Guid.NewGuid(); var date = DateTimeOffset.UtcNow.AddDays(-1);
            async Task Insert(Guid id, Guid tenant, Guid offerId, string name, string email, string phone, DateTimeOffset created)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "Checkouts" ("Id", "OrganizationId", "OfferId", "AccessHash", "Status", "Name", "Email", "Phone", "Document", "FieldsJson", "FormJson", "Price", "Currency", "CreatedAt", "ExpiresAt")
                    VALUES ({id}, {tenant}, {offerId}, {'A'.ToString().PadLeft(64, 'A')}, 'CREATED', {name}, {email}, {phone}, 'legacy-document', jsonb_build_object(), '[]', 10, 'BRL', {created}, {created.AddMinutes(30)})
                    """);
            }
            await Insert(first, org.Id, offer.Id, "Oldest profile", " Buyer@Example.test ", "11999990000", date);
            await Insert(second, org.Id, offer.Id, "Later snapshot", "buyer@example.test", "+55 (11) 99999-0000", date.AddHours(1));
            await Insert(third, org.Id, offer.Id, "Another email", "different@example.test", "+55 (11) 99999-0000", date.AddHours(2));
            await Insert(foreign, other.Id, offer2.Id, "Other tenant", "buyer@example.test", "+55 (11) 99999-0000", date);
            await db.Database.MigrateAsync();
            var customers = await db.Customers.AsNoTracking().ToListAsync(); Assert.Equal(3, customers.Count);
            var legacy = customers.Single(x => x.Id == first); Assert.Equal("Oldest profile", legacy.Name); Assert.Equal("11999990000", legacy.Phone); Assert.Null(legacy.NormalizedPhone); Assert.Equal("BUYER@EXAMPLE.TEST", legacy.NormalizedEmail);
            Assert.Equal("+5511999990000", customers.Single(x => x.Id == third).NormalizedPhone);
            var checkouts = await db.Checkouts.AsNoTracking().ToListAsync(); Assert.Equal(4, checkouts.Count); Assert.Equal(first, checkouts.Single(x => x.Id == second).CustomerId); Assert.Equal("Later snapshot", checkouts.Single(x => x.Id == second).Name); Assert.Equal("buyer@example.test", checkouts.Single(x => x.Id == second).Email);
            var invalidFk = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Checkouts\" SET \"CustomerId\" = {foreign} WHERE \"Id\" = {first}")); Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, invalidFk.SqlState);
            db.Customers.Add(new Customer { OrganizationId = org.Id, Name = "Duplicate", Email = "buyer@example.test", NormalizedEmail = "BUYER@EXAMPLE.TEST" });
            var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(duplicate.InnerException).SqlState);
        }
        finally
        {
            // Generated name only, exclusive to this test; never the shared application/test database.
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin); await drop.ExecuteNonQueryAsync();
        }
    }
}
