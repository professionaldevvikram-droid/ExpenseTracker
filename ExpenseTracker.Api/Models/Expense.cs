using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Models;

public class Expense
{
    public int Id { get; set; }

    [Range(0.01, 1000000)]
    public decimal Amount { get; set; }

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    [StringLength(200)]
    public string? Description { get; set; }

    public DateTime Date { get; set; } = DateTime.Today;
    public int UserId { get; set; }
    public AppUser? User { get; set; }
}