using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Models;

public class Category
{
    public int Id { get; set; }

    [Required, StringLength(50)]
    public string Name { get; set; } = string.Empty;

    // Hex color such as #4f46e5
    [Required, StringLength(7), RegularExpression("^#[0-9a-fA-F]{6}$")]
    public string Color { get; set; } = "#6366f1";

    // Optional monthly spending limit for this category
    [Range(0, 1000000000)]
    public decimal? MonthlyBudget { get; set; }
    public int UserId { get; set; }
    public AppUser? User { get; set; }
}