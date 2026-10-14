using System.Net;

namespace ExpenseTracker.Tests.Integration;

[Collection("api")]
public class PlatformTests
{
    private readonly ApiFactory _factory;

    public PlatformTests(ApiFactory factory) => _factory = factory;

    /* ---------- Everything with data needs a login ---------- */

    [Theory]
    [InlineData("GET", "/api/expenses")]
    [InlineData("GET", "/api/expenses/summary")]
    [InlineData("GET", "/api/expenses/export")]
    [InlineData("GET", "/api/expenses/1")]
    [InlineData("POST", "/api/expenses")]
    [InlineData("PUT", "/api/expenses/1")]
    [InlineData("DELETE", "/api/expenses/1")]
    [InlineData("GET", "/api/categories")]
    [InlineData("GET", "/api/categories/1")]
    [InlineData("POST", "/api/categories")]
    [InlineData("PUT", "/api/categories/1")]
    [InlineData("DELETE", "/api/categories/1")]
    [InlineData("GET", "/api/budgets/status")]
    [InlineData("GET", "/api/auth/me")]
    public async Task ProtectedEndpoints_RequireALogin(string method, string url)
    {
        var client = _factory.CreateClientWithIp();

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /* ---------- Health check ---------- */

    [Fact]
    public async Task Health_ReportsHealthy_WithoutALogin()
    {
        var response = await _factory.CreateClientWithIp().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    /* ---------- The web pages ---------- */

    [Theory]
    [InlineData("/")]
    [InlineData("/login.html")]
    [InlineData("/register.html")]
    public async Task Pages_AreServedWithoutALogin(string path)
    {
        var response = await _factory.CreateClientWithIp().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task TheHomePage_IsTheExpenseTracker()
    {
        var html = await _factory.CreateClientWithIp().GetStringAsync("/");

        Assert.Contains("Expense Tracker", html);
    }

    /* ---------- Security headers ---------- */

    [Fact]
    public async Task EveryResponse_CarriesTheSecurityHeaders()
    {
        var response = await _factory.CreateClientWithIp().GetAsync("/");

        Assert.True(response.Headers.TryGetValues("Content-Security-Policy", out var policy));
        Assert.Contains("default-src 'self'", policy!.Single());
        Assert.Contains("frame-ancestors 'none'", policy!.Single());

        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.True(response.Headers.Contains("Referrer-Policy"));
        Assert.True(response.Headers.Contains("Permissions-Policy"));
    }

    [Fact]
    public async Task ErrorResponses_CarryTheSecurityHeadersToo()
    {
        var response = await _factory.CreateClientWithIp().GetAsync("/api/expenses");     // 401 without a token

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Content-Type-Options"));
    }
}