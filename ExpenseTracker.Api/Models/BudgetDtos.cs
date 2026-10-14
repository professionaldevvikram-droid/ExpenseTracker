namespace ExpenseTracker.Api.Models;

public record BudgetStatus(
    int CategoryId,
    string Name,
    string Color,
    decimal Budget,
    decimal Spent);