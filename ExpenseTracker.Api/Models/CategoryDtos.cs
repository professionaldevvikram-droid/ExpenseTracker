using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Models;

public class CategoryRequest
{
	[Required, StringLength(50)]
	public string Name { get; set; } = string.Empty;

	[Required, RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "Color must look like #4f46e5.")]
	public string Color { get; set; } = "#6366f1";

	[Range(0, 1000000000)]
	public decimal? MonthlyBudget { get; set; }
}