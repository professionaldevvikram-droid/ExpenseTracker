using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Models;

public class RegisterRequest
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    public string Password { get; set; } = string.Empty;
}

public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public record UserInfo(int Id, string Name, string Email);

public record AuthResponse(string Token, DateTime ExpiresAt, UserInfo User);