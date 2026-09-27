using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zyven.Infrastructure;
namespace IntegrationTests;

public class CustomerTests
{
    [Fact]
    public async Task First_concurrent_checkouts_create_one_customer_and_share_the_link()
    {
        await using var app = new WebApplicationFactory<Program>();
        var fixture = await PublicCheckoutTests.Fixture(app); using var client = fixture.Client;
        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(i => client.PostAsJsonAsync($"/api/public/offers/{fixture.Slug}/checkouts", new { name = "Concurrent buyer", email = i % 2 == 0 ? "new@example.test" : " NEW@EXAMPLE.TEST ", phone = "+1 (202) 555-0123" })));
        foreach (var response in responses) Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ZyvenDbContext>();
        Assert.Equal(1, await db.Customers.CountAsync(x => x.OrganizationId == fixture.Org));
        var links = await db.Checkouts.Where(x => x.OrganizationId == fixture.Org).Select(x => x.CustomerId).ToListAsync(); Assert.Equal(6, links.Count); Assert.Single(links.Distinct());
    }
    [Fact]
    public async Task Customers_are_tenant_scoped_and_conflicts_never_merge_or_mutate_checkout()
    {
        await using var app = new WebApplicationFactory<Program>();
        var a = await PublicCheckoutTests.Fixture(app); using var owner = a.Client;
        var b = await PublicCheckoutTests.Fixture(app); using var other = b.Client;
        var path = $"/api/public/offers/{a.Slug}/checkouts";
        var first = await owner.PostAsJsonAsync(path, new { name = "Buyer A", email = "a@example.test", phone = "+5511999990000" }); first.EnsureSuccessStatusCode();
        var second = await owner.PostAsJsonAsync(path, new { name = "Buyer B", email = "b@example.test", phone = "+5511999990001" }); second.EnsureSuccessStatusCode();
        var conflict = await owner.PostAsJsonAsync(path, new { name = "Conflict", email = "a@example.test", phone = "+5511999990001" }); Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.DoesNotContain("b@example.test", await conflict.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(path, new { name = "Unknown email", email = "c@example.test", phone = "+5511999990001" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(path, new { name = "Local phone", email = "d@example.test", phone = "11999990000" })).StatusCode);
        var firstJson = await first.Content.ReadFromJsonAsync<JsonElement>(); var checkoutId = firstJson.GetProperty("id").GetGuid();
        owner.DefaultRequestHeaders.Add("Cookie", first.Headers.GetValues("Set-Cookie").Single().Split(';')[0]);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PatchAsJsonAsync($"/api/public/checkouts/{checkoutId}", new { name = "Changed", email = "a@example.test", phone = "+5511999990001" })).StatusCode);
        var unchanged = await (await owner.GetAsync($"/api/public/checkouts/{checkoutId}")).Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal("Buyer A", unchanged.GetProperty("name").GetString());
        var listPath = $"/api/organizations/{a.Org}/customers";
        var page = await (await owner.GetAsync(listPath + "?pageSize=1")).Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(2, page.GetProperty("total").GetInt32()); Assert.Equal(1, page.GetProperty("items").GetArrayLength());
        var customerId = page.GetProperty("items")[0].GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(listPath)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"{listPath}/{customerId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/organizations/{b.Org}/customers/{customerId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"{listPath}/{customerId}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.GetAsync(listPath + "?page=2147483647&pageSize=100")).StatusCode);
        using var anonymous = app.CreateClient(); Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(listPath)).StatusCode);
        (await other.PostAsJsonAsync($"/api/public/offers/{b.Slug}/checkouts", new { name = "Other tenant", email = "a@example.test", phone = "+5511999990000" })).EnsureSuccessStatusCode();
        var otherPage = await (await other.GetAsync($"/api/organizations/{b.Org}/customers")).Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(1, otherPage.GetProperty("total").GetInt32());
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ZyvenDbContext>();
        var before = await db.Checkouts.AsNoTracking().SingleAsync(x => x.Id == checkoutId);
        (await owner.PatchAsJsonAsync($"/api/public/checkouts/{checkoutId}", new { name = "New contact", email = "new@example.test" })).EnsureSuccessStatusCode();
        var after = await db.Checkouts.AsNoTracking().SingleAsync(x => x.Id == checkoutId); Assert.NotEqual(before.CustomerId, after.CustomerId);
        Assert.Equal("Buyer A", (await db.Customers.FindAsync(before.CustomerId))!.Name);
    }
    [Fact]
    public async Task Checkout_identification_deduplicates_without_disclosing_or_overwriting_existing_profile()
    {
        await using var app = new WebApplicationFactory<Program>();
        var fixture = await PublicCheckoutTests.Fixture(app); using var owner = fixture.Client;
        var path = $"/api/public/offers/{fixture.Slug}/checkouts";
        var first = await owner.PostAsJsonAsync(path, new { name = "Original private name", email = " Buyer@Example.test ", phone = "+55 (11) 99999-0000", document = "PRIVATE123" }); first.EnsureSuccessStatusCode();
        var repeats = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => owner.PostAsJsonAsync(path, new { name = "Different name", email = "buyer@example.test" })));
        foreach (var repeat in repeats) { repeat.EnsureSuccessStatusCode(); var body = await repeat.Content.ReadAsStringAsync(); Assert.DoesNotContain("PRIVATE123", body); Assert.DoesNotContain("Original private", body); Assert.DoesNotContain("99999", body); }
        var listed = await owner.GetAsync($"/api/organizations/{fixture.Org}/customers"); Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        var page = await listed.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(1, page.GetProperty("total").GetInt32());
        var customer = page.GetProperty("items")[0]; Assert.Equal("Original private name", customer.GetProperty("name").GetString()); Assert.Equal("+5511999990000", customer.GetProperty("phone").GetString());
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ZyvenDbContext>();
        var ids = await db.Checkouts.Where(x => x.OrganizationId == fixture.Org).Select(x => EF.Property<Guid>(x, "CustomerId")).Distinct().ToListAsync(); Assert.Single(ids);
    }
}
