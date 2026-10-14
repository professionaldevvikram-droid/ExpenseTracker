using ExpenseTracker.Api.Common;
using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<UserInfo> GetCurrentUserAsync(int userId);
}

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly ITokenService _tokens;
    private readonly IPasswordHasher<AppUser> _hasher;
    private readonly ILogger<AuthService> _logger;

    public AuthService(AppDbContext db, ITokenService tokens, IPasswordHasher<AppUser> hasher, ILogger<AuthService> logger)
    {
        _db = db;
        _tokens = tokens;
        _hasher = hasher;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var email = NormalizeEmail(request.Email);

        if (await _db.Users.AnyAsync(u => u.Email == email))
            throw new ConflictException("An account with this email already exists.");

        var user = new AppUser { Name = request.Name.Trim(), Email = email };
        user.PasswordHash = _hasher.HashPassword(user, request.Password);   // salted hash, never the password

        _db.Users.Add(user);

        // The new account starts with its own copy of the default categories.
        // Everything is saved in one go, so either the user and categories all exist, or none do.
        foreach (var (name, color) in DefaultCategories.All)
        {
            _db.Categories.Add(new Category { Name = name, Color = color, User = user });
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Two people registering the same email at the same moment: the unique index stops the second one
            throw new ConflictException("An account with this email already exists.");
        }
        _logger.LogInformation("New account registered: user {UserId}", user.Id);

        return CreateResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var email = NormalizeEmail(request.Email);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        var verified = user is not null &&
            _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) != PasswordVerificationResult.Failed;

        // One message for both "no such email" and "wrong password", so nobody can find out which emails exist
        if (!verified)
        {
            _logger.LogWarning("Failed login attempt");
            throw new UnauthorizedException("Invalid email or password.");
        }

        _logger.LogInformation("User {UserId} logged in", user!.Id);
        return CreateResponse(user);
    }

    public async Task<UserInfo> GetCurrentUserAsync(int userId)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new UnauthorizedException("Please log in again.");

        return new UserInfo(user.Id, user.Name, user.Email);
    }

    private AuthResponse CreateResponse(AppUser user)
    {
        var (token, expiresAt) = _tokens.CreateToken(user);
        return new AuthResponse(token, expiresAt, new UserInfo(user.Id, user.Name, user.Email));
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}