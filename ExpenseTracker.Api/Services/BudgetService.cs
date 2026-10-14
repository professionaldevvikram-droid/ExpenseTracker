using System.Globalization;
using ExpenseTracker.Api.Common;
using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Services;

public interface IBudgetService
{
    Task<List<BudgetStatus>> GetStatusAsync(string? month);
}

public class BudgetService : IBudgetService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _user;

    public BudgetService(AppDbContext db, ICurrentUser user)
    {
        _db = db;
        _user = user;
    }

    // month looks like "2026-10". When it is empty, the current month is used.
    public async Task<List<BudgetStatus>> GetStatusAsync(string? month)
    {
        DateTime start;
        if (string.IsNullOrWhiteSpace(month))
        {
            var today = DateTime.Today;
            start = new DateTime(today.Year, today.Month, 1);
        }
        else if (!DateTime.TryParseExact(month + "-01", "yyyy-MM-dd",
                     CultureInfo.InvariantCulture, DateTimeStyles.None, out start))
        {
            throw new BadRequestException("Month must look like 2026-10.");
        }

        var end = start.AddMonths(1);
        var userId = _user.Id;

        // One SQL query: total spent per category in that month
        var spentByCategory = await _db.Expenses
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.Date >= start && e.Date < end)
            .GroupBy(e => e.CategoryId)
            .Select(g => new { CategoryId = g.Key, Spent = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Spent);

        // Only this user's categories that have a budget
        var categories = await _db.Categories
            .AsNoTracking()
            .Where(c => c.UserId == userId && c.MonthlyBudget != null && c.MonthlyBudget > 0)
            .OrderBy(c => c.Name)
            .ToListAsync();

        var result = new List<BudgetStatus>();
        foreach (var c in categories)
        {
            spentByCategory.TryGetValue(c.Id, out var spent);
            result.Add(new BudgetStatus(c.Id, c.Name, c.Color, c.MonthlyBudget!.Value, spent));
        }

        return result;
    }
}