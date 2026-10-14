using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Models;

// What the API sends to the browser (same field names as before, plus a few new ones)
public record ExpenseResponse(
    int Id,
    decimal Amount,
    int CategoryId,
    string Category,
    string CategoryColor,
    string? Description,
    DateTime Date);

// What the browser sends when adding or editing an expense
public class ExpenseRequest
{
    [Range(0.01, 1000000)]
    public decimal Amount { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Please choose a category.")]
    public int CategoryId { get; set; }

    [StringLength(200)]
    public string? Description { get; set; }

    public DateTime Date { get; set; }
}