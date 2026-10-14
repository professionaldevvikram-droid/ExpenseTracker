using System.Text;
using ExpenseTracker.Api.Common;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Services;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ExpenseTracker.Tests.Unit;

public class TokenServiceTests
{
    private const string Key = "unit-test-signing-key-that-is-long-enough-1234567890";
    private const string OtherKey = "a-completely-different-signing-key-1234567890-abcdef";

    private static TokenService CreateService() => new(new JwtOptions
    {
        Key = Key,
        Issuer = "TestIssuer",
        Audience = "TestAudience",
        ExpiryMinutes = 60
    });

    private static AppUser Asha() => new() { Id = 42, Name = "Asha", Email = "asha@example.com" };

    // The same checks the real API applies to every incoming token
    private static TokenValidationParameters ParametersFor(string key) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = "TestIssuer",
        ValidateAudience = true,
        ValidAudience = "TestAudience",
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
        ValidateLifetime = true
    };

    [Fact]
    public void CreateToken_ContainsTheUsersDetails()
    {
        var (token, _) = CreateService().CreateToken(Asha());

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token);

        Assert.Equal("42", jwt.GetClaim("sub").Value);
        Assert.Equal("asha@example.com", jwt.GetClaim("email").Value);
        Assert.Equal("Asha", jwt.GetClaim("name").Value);
    }

    [Fact]
    public void CreateToken_ExpiresAfterTheConfiguredTime()
    {
        var (_, expiresAt) = CreateService().CreateToken(Asha());

        Assert.InRange(expiresAt, DateTime.UtcNow.AddMinutes(59), DateTime.UtcNow.AddMinutes(61));
    }

    [Fact]
    public async Task Token_IsAcceptedWhenTheKeyMatches()
    {
        var (token, _) = CreateService().CreateToken(Asha());

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, ParametersFor(Key));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Token_IsRejectedWhenTheKeyIsDifferent()
    {
        var (token, _) = CreateService().CreateToken(Asha());

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, ParametersFor(OtherKey));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Token_IsRejectedWhenSomeoneEditsIt()
    {
        var (token, _) = CreateService().CreateToken(Asha());

        // Change one character in the middle section, the part that holds the user id
        var parts = token.Split('.');
        parts[1] = parts[1].Remove(5, 1).Insert(5, parts[1][5] == 'a' ? "b" : "a");
        var tampered = string.Join('.', parts);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(tampered, ParametersFor(Key));

        Assert.False(result.IsValid);
    }
}