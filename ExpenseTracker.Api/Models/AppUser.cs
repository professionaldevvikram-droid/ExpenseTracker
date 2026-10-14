using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Models;

public class AppUser
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(256)]
    public string Email { get; set; } = string.Empty;       // always stored in lower case

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}