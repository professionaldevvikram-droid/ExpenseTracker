using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ExpenseTracker.Api.Common;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Services;

namespace ExpenseTracker.Tests.Integration;

[Collection("api")]
public class AuthApiTests
{
    private readonly ApiFactory _factory;

    public AuthApiTests(ApiFactory factory) => _factory = factory;

    private static RegisterRequest NewRegistration(string? password = null, string? email = null) => new()
    {
        Name = "Asha",
        Email = email ?? $"{Guid.NewGuid():N}@example.com",
        Password = password ?? TestData.Password
    };

    // Creates a token the same way the API does, but with a key and lifetime of our choosing
    private static string TokenSignedWith(string key, int userId, int expiryMinutes = 60) =>
        new TokenService(new JwtOptions { Key = key, ExpiryMinutes = expiryMinutes })
            .CreateToken(new AppUser { Id = userId, Name = "Someone", Email = "someone@example.com" })
            .Token;

    private static Task<HttpResponseMessage> CallMeWithAsync(HttpClient client, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(request);
    }

    /* ---------- Registering ---------- */

    [Fact]
    public async Task Register_ReturnsATokenAndTheUser_WithALowerCaseEmail()
    {
        var client = _factory.CreateClientWithIp();
        var request = NewRegistration(email: $"Asha-{Guid.NewGuid():N}@Example.COM");

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.False(string.IsNullOrWhiteSpace(auth.Token));
        Assert.Equal("Asha", auth.User.Name);
        Assert.Equal(request.Email.ToLowerInvariant(), auth.User.Email);
    }

    [Fact]
    public async Task Register_RejectsAShortPassword_WithAClearMessage()
    {
        var client = _factory.CreateClientWithIp();

        var response = await client.PostAsJsonAsync("/api/auth/register", NewRegistration(password: "short"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Password must be at least 8 characters.", await response.MessageAsync());
    }

    [Fact]
    public async Task Register_RejectsAnInvalidEmail()
    {
        var client = _factory.CreateClientWithIp();

        var response = await client.PostAsJsonAsync("/api/auth/register", NewRegistration(email: "not-an-email"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(await response.MessageAsync()));
    }

    [Fact]
    public async Task Register_RejectsAnEmailThatIsAlreadyUsed_IgnoringCase()
    {
        var client = _factory.CreateClientWithIp();
        var email = $"{Guid.NewGuid():N}@example.com";
        (await client.PostAsJsonAsync("/api/auth/register", NewRegistration(email: email))).EnsureSuccessStatusCode();

        var again = await client.PostAsJsonAsync("/api/auth/register", NewRegistration(email: email.ToUpperInvariant()));

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("An account with this email already exists.", await again.MessageAsync());
    }

    [Fact]
    public async Task Register_GivesTheNewUserTheDefaultCategories()
    {
        var user = await _factory.RegisterUserAsync();

        var categories = await user.Client.GetCategoriesAsync();

        Assert.Equal(
            DefaultCategories.All.Select(c => c.Name).Order(),
            categories.Select(c => c.Name).Order());
        Assert.All(categories, c => Assert.Equal(0, c.ExpenseCount));
    }

    /* ---------- Logging in ---------- */

    [Fact]
    public async Task Login_WithTheRightPassword_GivesATokenThatWorks()
    {
        var registered = await _factory.RegisterUserAsync("Asha");
        var client = _factory.CreateClientWithIp();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = registered.Email.ToUpperInvariant(), Password = TestData.Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;

        var me = await CallMeWithAsync(client, auth.Token);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(registered.Id, (await me.Content.ReadFromJsonAsync<UserInfo>())!.Id);
    }

    [Theory]
    [InlineData(true)]      // a real account with the wrong password
    [InlineData(false)]     // an email nobody registered
    public async Task Login_FailsWithTheSameMessage_WhateverWentWrong(bool accountExists)
    {
        var registered = await _factory.RegisterUserAsync();
        var client = _factory.CreateClientWithIp();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = accountExists ? registered.Email : $"{Guid.NewGuid():N}@example.com",
            Password = "wrong-password"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Invalid email or password.", await response.MessageAsync());
    }

    /* ---------- Tokens ---------- */

    [Fact]
    public async Task Me_ReturnsTheLoggedInUser()
    {
        var user = await _factory.RegisterUserAsync("Asha");

        var info = await user.Client.GetFromJsonAsync<UserInfo>("/api/auth/me");

        Assert.Equal(user.Email, info!.Email);
        Assert.Equal("Asha", info.Name);
    }

    [Fact]
    public async Task Me_RequiresAToken()
    {
        var response = await _factory.CreateClientWithIp().GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_RejectsATokenThatIsNotAToken()
    {
        var response = await CallMeWithAsync(_factory.CreateClientWithIp(), "this-is-not-a-jwt");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Tokens_AreOnlyAcceptedWhenSignedWithTheRealKey_AndNotExpired()
    {
        var user = await _factory.RegisterUserAsync();
        var client = _factory.CreateClientWithIp();

        // The control case: the same helper with the real key works, so the helper itself is fine
        var good = await CallMeWithAsync(client, TokenSignedWith(ApiFactory.JwtKey, user.Id));
        Assert.Equal(HttpStatusCode.OK, good.StatusCode);

        // The right user id, but signed with someone else's key
        var forged = await CallMeWithAsync(client, TokenSignedWith("another-completely-different-signing-key-1234567890-xyz", user.Id));
        Assert.Equal(HttpStatusCode.Unauthorized, forged.StatusCode);

        // The right key, but expired half an hour ago
        var expired = await CallMeWithAsync(client, TokenSignedWith(ApiFactory.JwtKey, user.Id, expiryMinutes: -30));
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
    }

    [Fact]
    public async Task Me_RejectsAValidTokenForAnAccountThatNoLongerExists()
    {
        var token = TokenSignedWith(ApiFactory.JwtKey, userId: 999_999_999);

        var response = await CallMeWithAsync(_factory.CreateClientWithIp(), token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Please log in again.", await response.MessageAsync());
    }

    /* ---------- Rate limiting ---------- */

    [Fact]
    public async Task Login_IsLimitedToTenAttemptsAMinute_PerIpAddress()
    {
        var attacker = _factory.CreateClientWithIp("10.250.0.1");     // an address no other test uses
        var login = new LoginRequest { Email = "nobody@example.com", Password = "wrong-password" };

        for (var attempt = 1; attempt <= 10; attempt++)
        {
            var response = await attacker.PostAsJsonAsync("/api/auth/login", login);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var blocked = await attacker.PostAsJsonAsync("/api/auth/login", login);
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.Contains("Too many attempts", await blocked.MessageAsync());
        Assert.True(blocked.Headers.Contains("Retry-After"));

        // Someone on a different address is not affected
        var someoneElse = _factory.CreateClientWithIp("10.250.0.2");
        var other = await someoneElse.PostAsJsonAsync("/api/auth/login", login);
        Assert.Equal(HttpStatusCode.Unauthorized, other.StatusCode);
    }
}