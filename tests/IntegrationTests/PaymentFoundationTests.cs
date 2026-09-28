using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Zyven.Application;
using Zyven.Domain;
using Zyven.Infrastructure;
using System.Net;
using System.Net.Http.Json;
namespace IntegrationTests;

public class PaymentFoundationTests
{
    [Fact]
    public async Task Owner_connects_the_organization_to_a_syncpay_recipient()
    {
        await using var app = new WebApplicationFactory<Program>();
        var fixture = await PublicCheckoutTests.Fixture(app); using var client = fixture.Client;

        var response = await client.PutAsJsonAsync($"/api/organizations/{fixture.Org}/payment-account", new { providerRecipientId = $"seller-{Guid.NewGuid():N}" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = app.Services.CreateScope();
        var merchant = await scope.ServiceProvider.GetRequiredService<ZyvenDbContext>().MerchantAccounts.SingleAsync(x => x.OrganizationId == fixture.Org);
        Assert.Equal("ACTIVE", merchant.Status);
        Assert.StartsWith("seller-", merchant.ProviderRecipientId);
    }

    [Fact]
    public async Task Organization_creation_provisions_pending_merchant_and_runtime_cannot_charge()
    {
        await using var app = new WebApplicationFactory<Program>();
        var fixture = await PublicCheckoutTests.Fixture(app); using var client = fixture.Client;
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ZyvenDbContext>();
        var merchant = await db.Set<MerchantAccount>().SingleAsync(x => x.OrganizationId == fixture.Org);
        Assert.Equal("PENDING", merchant.Status);
        var processor = scope.ServiceProvider.GetRequiredService<IPaymentProcessor>();
        Assert.False(processor.Capabilities.Pix);
        Assert.Equal(PaymentOperationError.Unavailable, (await processor.CreatePixAsync(null!, default)).Error);
        Assert.False(await db.Set<Payment>().AnyAsync(x => x.OrganizationId == fixture.Org));
    }

    [Fact]
    public async Task Upgrade_preserves_checkout_and_enforces_payment_tenant_links_and_uniqueness()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Database") ?? throw new InvalidOperationException("Test database required.");
        var databaseName = "zyven_payment_upgrade_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connection) { Database = databaseName, Pooling = false };
            await using var db = new ZyvenDbContext(new DbContextOptionsBuilder<ZyvenDbContext>().UseNpgsql(builder.ConnectionString).Options);
            await db.GetService<IMigrator>().MigrateAsync("20260927184003_Customers");
            var now = DateTimeOffset.UtcNow;
            var org = new Organization { Name = "Existing" }; var other = new Organization { Name = "Other" }; db.Organizations.AddRange(org, other); await db.SaveChangesAsync();
            var product = new Product { OrganizationId = org.Id, Name = "Product", Slug = "payment-test" }; db.Products.Add(product); await db.SaveChangesAsync();
            var offer = new Offer { OrganizationId = org.Id, ProductId = product.Id, Name = "Offer", Slug = "payment-test", Price = 10 }; var offer2 = new Offer { OrganizationId = org.Id, ProductId = product.Id, Name = "Other", Slug = "payment-other", Price = 10 }; db.Offers.AddRange(offer, offer2);
            var customer = new Customer { OrganizationId = org.Id, Name = "Buyer", Email = "a@example.test", NormalizedEmail = "A@EXAMPLE.TEST" }; var customer2 = new Customer { OrganizationId = org.Id, Name = "Other", Email = "b@example.test", NormalizedEmail = "B@EXAMPLE.TEST" }; var foreignCustomer = new Customer { OrganizationId = other.Id, Name = "Foreign", Email = "a@example.test", NormalizedEmail = "A@EXAMPLE.TEST" }; db.Customers.AddRange(customer, customer2, foreignCustomer); await db.SaveChangesAsync();
            var checkout = new CheckoutSession { OrganizationId = org.Id, CustomerId = customer.Id, OfferId = offer.Id, Price = 10, Currency = "BRL", CreatedAt = now, ExpiresAt = now.AddMinutes(30) }; db.Checkouts.Add(checkout); await db.SaveChangesAsync();
            await db.Database.MigrateAsync();
            var merchants = await db.Set<MerchantAccount>().ToListAsync(); Assert.Equal(2, merchants.Count); Assert.All(merchants, m => Assert.Equal("PENDING", m.Status));
            Assert.Empty(await db.Set<Payment>().ToListAsync());
            var merchant = merchants.Single(x => x.OrganizationId == org.Id); var otherMerchant = merchants.Single(x => x.OrganizationId == other.Id);
            checkout.CustomerId = customer2.Id; await db.SaveChangesAsync(); // remains mutable before a payment
            merchant.Status = "ACTIVE"; await db.SaveChangesAsync(); // fixture only; runtime has no approval operation
            var payment = Payment.Prepare(checkout, merchant, 1.25m, now); db.Set<Payment>().Add(payment); await db.SaveChangesAsync();
            Assert.Equal(8.75m, (await db.Set<Payment>().AsNoTracking().SingleAsync()).NetAmount);
            async Task Rejected(FormattableString sql, string code)
            {
                var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(sql)); Assert.Equal(code, error.SqlState);
            }
            await Rejected($"UPDATE \"Payments\" SET \"MerchantAccountId\" = {otherMerchant.Id} WHERE \"Id\" = {payment.Id}", PostgresErrorCodes.ForeignKeyViolation);
            await Rejected($"UPDATE \"Payments\" SET \"CustomerId\" = {foreignCustomer.Id} WHERE \"Id\" = {payment.Id}", PostgresErrorCodes.ForeignKeyViolation);
            await Rejected($"UPDATE \"Payments\" SET \"CustomerId\" = {customer.Id} WHERE \"Id\" = {payment.Id}", PostgresErrorCodes.ForeignKeyViolation);
            await Rejected($"UPDATE \"Payments\" SET \"OfferId\" = {offer2.Id} WHERE \"Id\" = {payment.Id}", PostgresErrorCodes.ForeignKeyViolation);
            await Rejected($"UPDATE \"Checkouts\" SET \"CustomerId\" = {customer.Id} WHERE \"Id\" = {checkout.Id}", PostgresErrorCodes.ForeignKeyViolation);
            await Rejected($"UPDATE \"Payments\" SET \"GrossAmount\" = {-1m} WHERE \"Id\" = {payment.Id}", PostgresErrorCodes.CheckViolation);
            await Rejected($"UPDATE \"Payments\" SET \"NetAmount\" = {10m} WHERE \"Id\" = {payment.Id}", PostgresErrorCodes.CheckViolation);
            await Rejected($"UPDATE \"Payments\" SET \"Status\" = {"UNKNOWN"} WHERE \"Id\" = {payment.Id}", PostgresErrorCodes.CheckViolation);
            await Rejected($"UPDATE \"MerchantAccounts\" SET \"Status\" = {"APPROVED"} WHERE \"Id\" = {merchant.Id}", PostgresErrorCodes.CheckViolation);
            await Rejected($"INSERT INTO \"MerchantAccounts\" (\"Id\", \"OrganizationId\", \"Status\", \"CreatedAt\", \"UpdatedAt\") VALUES ({Guid.NewGuid()}, {org.Id}, 'PENDING', {now}, {now})", PostgresErrorCodes.UniqueViolation);
            var retry = Payment.Prepare(checkout, merchant, 1.25m, now); db.Set<Payment>().Add(retry); await db.SaveChangesAsync();
            await Rejected($"UPDATE \"Payments\" SET \"ExternalReference\" = {payment.ExternalReference} WHERE \"Id\" = {retry.Id}", PostgresErrorCodes.UniqueViolation);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Payments\" SET \"Provider\" = 'sandbox-a', \"ProviderTransactionId\" = 'same-id' WHERE \"Id\" = {payment.Id}");
            await Rejected($"UPDATE \"Payments\" SET \"Provider\" = 'sandbox-a', \"ProviderTransactionId\" = 'same-id' WHERE \"Id\" = {retry.Id}", PostgresErrorCodes.UniqueViolation);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Payments\" SET \"Provider\" = 'sandbox-b', \"ProviderTransactionId\" = 'same-id' WHERE \"Id\" = {retry.Id}");
            // Exercise custom constraint teardown/recreation only in this test's disposable database.
            db.ChangeTracker.Clear(); await db.GetService<IMigrator>().MigrateAsync("20260927184003_Customers"); await db.Database.MigrateAsync();
            Assert.Equal(1, await db.Checkouts.CountAsync()); Assert.Equal(2, await db.Set<MerchantAccount>().CountAsync());
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin); await drop.ExecuteNonQueryAsync();
        }
    }
}
