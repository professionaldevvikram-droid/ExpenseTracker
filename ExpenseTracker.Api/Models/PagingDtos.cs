namespace ExpenseTracker.Api.Models;

// One page of results, plus totals for everything that matches the filters
public record PagedResult<T>(
    List<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    decimal TotalAmount);

// Numbers for the summary cards and charts (all expenses)
public record CategoryTotal(int CategoryId, string Name, string Color, decimal Total);
public record MonthTotal(string Month, decimal Total);        // Month looks like "2026-10"

public record ExpenseSummary(
    decimal TotalSpent,
    int Count,
    decimal ThisMonth,
    List<CategoryTotal> ByCategory,
    List<MonthTotal> Months);

// A category together with how many expenses use it
public record CategoryResponse(
    int Id,
    string Name,
    string Color,
    decimal? MonthlyBudget,
    int ExpenseCount);