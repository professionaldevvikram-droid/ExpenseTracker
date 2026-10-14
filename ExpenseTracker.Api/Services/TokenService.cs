using System.Security.Claims;
using System.Text;
using ExpenseTracker.Api.Common;
using ExpenseTracker.Api.Models;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ExpenseTracker.Api.Services;

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateToken(AppUser user);
}

public class TokenService : ITokenService
{
    private readonly JwtOptions _jwt;

    public TokenService(JwtOptions jwt)
    {
        _jwt = jwt;
    }

    public (string Token, DateTime ExpiresAt) CreateToken(AppUser user)
    {
        var expires = DateTime.UtcNow.AddMinutes(_jwt.ExpiryMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));

        var descriptor = new SecurityTokenDescriptor
        {
            // The "claims" are the facts stored inside the token
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("sub", user.Id.ToString()),
                new Claim("email", user.Email),
                new Claim("name", user.Name)
            }),
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            Expires = expires,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return (token, expires);
    }
}