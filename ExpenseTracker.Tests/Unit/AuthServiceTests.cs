using ExpenseTracker.Api.Common;
using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Services;
using ExpenseTracker.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ExpenseTracker.Tests.Unit;

public class AuthServiceTests
{
    private const string Password = "Passw0rd!";

    private static AuthService CreateService(AppDbContext db) => new(
        db,
        new TokenService(new JwtOptions { Key = "unit-test-signing-key-that-is-long-enough-1234567890" }),
        new PasswordHasher<AppUser>(),
        NullLogger<AuthService>.Instance);

    private static RegisterRequest Registration(string email = "asha@example.com") =>
        new() { Name = "Asha", Email = email, Password = Password };

    [Fact]
    public async Task Register_CreatesTheUser_AndReturnsAToken()
    {
        using var db = TestDb.Create();

        var response = await CreateService(db).RegisterAsync(Registration("Asha@Example.com"));

        Assert.False(string.IsNullOrWhiteSpace(response.Token));
        Assert.Equal("asha@example.com", response.User.Email);                  // stored in lower case
        Assert.Single(db.Users);
    }

    [Fact]
    public async Task Register_StoresAHashAndNeverThePassword()
    {
        using var db = TestDb.Create();

        await CreateService(db).RegisterAsync(Registration());

        var user = await db.Users.SingleAsync();
        Assert.NotEqual(Password, user.PasswordHash);
        Assert.DoesNotContain(Password, user.PasswordHash);
    }

    [Fact]
    public async Task Register_GivesTheNewUserTheDefaultCategories()
    {
        using var db = TestDb.Create();

        var response = await CreateService(db).RegisterAsync(Registration());

        var names = await db.Categories
            .Where(c => c.UserId == response.User.Id)
            .Select(c => c.Name)
            .ToListAsync();

        Assert.Equal(DefaultCategories.All.Select(c => c.Name).Order(), names.Order());
    }

    [Fact]
    public async Task Register_GivesEachUserTheirOwnCategories()
    {
        using var db = TestDb.Create();
        var service = CreateService(db);

        var first = await service.RegisterAsync(Registration("one@example.com"));
        var second = await service.RegisterAsync(Registration("two@example.com"));

        var perUser = DefaultCategories.All.Length;
        Assert.Equal(perUser, await db.Categories.CountAsync(c => c.UserId == first.User.Id));
        Assert.Equal(perUser, await db.Categories.CountAsync(c => c.UserId == second.User.Id));
    }

    [Fact]
    public async Task Register_RejectsAnEmailThatIsAlreadyUsed_IgnoringCase()
    {
        using var db = TestDb.Create();
        var service = CreateService(db);
        await service.RegisterAsync(Registration("asha@example.com"));

        await Assert.ThrowsAsync<ConflictException>(() => service.RegisterAsync(Registration("ASHA@Example.com")));

        Assert.Single(db.Users);
    }

    [Fact]
    public async Task Login_WorksWithTheCorrectPassword_AndAnyEmailCasing()
    {
        using var db = TestDb.Create();
        var service = CreateService(db);
        await service.RegisterAsync(Registration());

        var response = await service.LoginAsync(new LoginRequest { Email = "ASHA@example.com", Password = Password });

        Assert.False(string.IsNullOrWhiteSpace(response.Token));
        Assert.Equal("Asha", response.User.Name);
    }

    [Fact]
    public async Task Login_GivesTheSameErrorForAWrongPasswordAndAnUnknownEmail()
    {
        using var db = TestDb.Create();
        var service = CreateService(db);
        await service.RegisterAsync(Registration());

        var wrongPassword = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            service.LoginAsync(new LoginRequest { Email = "asha@example.com", Password = "wrong-password" }));

        var unknownEmail = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            service.LoginAsync(new LoginRequest { Email = "nobody@example.com", Password = Password }));

        // The same message, so nobody can find out which emails have accounts
        Assert.Equal(wrongPassword.Message, unknownEmail.Message);
    }
}