using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
namespace IntegrationTests;

public class BuyerAreaTests
{
    [Fact]
    public async Task Unknown_email_receives_neutral_response_and_no_session()
    {
        await using var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient(new() { HandleCookies = false });
        client.DefaultRequestHeaders.Add("X-Zyven-Client", "web");
        var response = await client.PostAsJsonAsync("/api/buyer/auth/request-code", new { email = $"missing-{Guid.NewGuid():N}@example.test" });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/buyer/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/buyer/purchases")).StatusCode);
    }

    [Fact]
    public async Task Invalid_code_uses_single_public_error()
    {
        await using var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient(new() { HandleCookies = false });
        client.DefaultRequestHeaders.Add("X-Zyven-Client", "web");
        var response = await client.PostAsJsonAsync("/api/buyer/auth/verify-code", new { email = "missing@example.test", code = "123456" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
