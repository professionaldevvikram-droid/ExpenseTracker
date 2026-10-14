using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ExpenseTracker.Api.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace ExpenseTracker.Tests.Integration;

// Every test class marked [Collection("api")] shares ONE factory, so one test database
[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<ApiFactory> { }

public static class TestData
{
    public const string Password = "Passw0rd!";
}

public record ErrorBody(string Message);

// A registered, logged-in user. Their client already sends the token.
public record TestUser(HttpClient Client, AuthResponse Auth, string Email)
{
    public int Id => Auth.User.Id;
}

// If a request carries an "X-Test-Ip" header, the API treats it as coming from that address
public class TestIpStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            if (context.Request.Headers.TryGetValue("X-Test-Ip", out var value) &&
                IPAddress.TryParse(value.ToString(), out var address))
            {
                context.Connection.RemoteIpAddress = address;
            }

            await nextMiddleware();
        });

        next(app);
    };
}

public static class TestExtensions
{
    private static int _clients;

    // A client that pretends to be on its own IP address
    public static HttpClient CreateClientWithIp(this ApiFactory factory, string? ip = null)
    {
        var client = factory.CreateClient();
        var n = Interlocked.Increment(ref _clients);
        client.DefaultRequestHeaders.Add("X-Test-Ip", ip ?? $"10.1.{n / 256 % 256}.{n % 256}");
        return client;
    }

    // Registers a brand-new user with a random email and returns a client that is logged in as them
    public static async Task<TestUser> RegisterUserAsync(this ApiFactory factory, string name = "Test User")
    {
        var client = factory.CreateClientWithIp();
        var email = $"{Guid.NewGuid():N}@example.com";

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Name = name, Email = email, Password = TestData.Password });
        response.EnsureSuccessStatusCode();

        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        return new TestUser(client, auth, email);
    }

    public static async Task<List<CategoryResponse>> GetCategoriesAsync(this HttpClient client) =>
        (await client.GetFromJsonAsync<List<CategoryResponse>>("/api/categories"))!;

    public static async Task<CategoryResponse> CategoryNamedAsync(this HttpClient client, string name) =>
        (await client.GetCategoriesAsync()).Single(c => c.Name == name);

    public static async Task<ExpenseResponse> AddExpenseAsync(
        this HttpClient client, int categoryId, decimal amount, DateTime? date = null, string? description = null)
    {
        var response = await client.PostAsJsonAsync("/api/expenses", new ExpenseRequest
        {
            Amount = amount,
            CategoryId = categoryId,
            Date = date ?? DateTime.Today,
            Description = description
        });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ExpenseResponse>())!;
    }

    public static async Task<PagedResult<ExpenseResponse>> ListExpensesAsync(this HttpClient client, string query = "") =>
        (await client.GetFromJsonAsync<PagedResult<ExpenseResponse>>("/api/expenses" + query))!;

    // Reads the { "message": "..." } body that every API error has
    public static async Task<string> MessageAsync(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ErrorBody>())!.Message;
}