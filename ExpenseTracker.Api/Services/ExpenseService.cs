using System.Globalization;
using System.Linq.Expressions;
using ExpenseTracker.Api.Common;
using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Services;

public interface IExpenseService
{
    Task<PagedResult<ExpenseResponse>> GetPagedAsync(ExpenseQuery filter);
    Task<ExpenseSummary> GetSummaryAsync();
    Task<ExpenseResponse> GetByIdAsync(int id);
    Task<ExpenseResponse> CreateAsync(ExpenseRequest request);
    Task UpdateAsync(int id, ExpenseRequest request);
    Task DeleteAsync(int id);
}

public class ExpenseService : IExpenseService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _user;

    public ExpenseService(AppDbContext db, ICurrentUser user)
    {
        _db = db;
        _user = user;
    }

    // How an Expense becomes an ExpenseResponse. EF Core turns this into a SQL JOIN.
    private static readonly Expression<Func<Expense, ExpenseResponse>> ToResponse = e =>
        new ExpenseResponse(
            e.Id,
            e.Amount,
            e.CategoryId,
            e.Category!.Name,
            e.Category.Color,
            e.Description,
            e.Date);

    // Every query starts here, so a user can only ever reach their own expenses
    private IQueryable<Expense> MyExpenses()
    {
        var userId = _user.Id;
        return _db.Expenses.Where(e => e.UserId == userId);
    }

    public async Task<PagedResult<ExpenseResponse>> GetPagedAsync(ExpenseQuery filter)
    {
        filter.ThrowIfInvalid();

        var query = MyExpenses().AsNoTracking().ApplyFilters(filter);

        // Two small queries: how many rows match, and their total amount
        var totalCount = await query.CountAsync();
        var totalAmount = await query.SumAsync(e => (decimal?)e.Amount) ?? 0m;

        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        var page = Math.Clamp(filter.Page, 1, totalPages);

        // Only the rows for this page leave the database
        var items = await query
            .ApplySort(filter.Sort)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToResponse)
            .ToListAsync();

        return new PagedResult<ExpenseResponse>(items, page, pageSize, totalCount, totalPages, totalAmount);
    }

    public async Task<ExpenseSummary> GetSummaryAsync()
    {
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var nextMonthStart = monthStart.AddMonths(1);
        var trendStart = monthStart.AddMonths(-5);             // the last 6 months including this one

        var expenses = MyExpenses().AsNoTracking();

        var count = await expenses.CountAsync();
        var totalSpent = await expenses.SumAsync(e => (decimal?)e.Amount) ?? 0m;

        var thisMonth = await expenses
            .Where(e => e.Date >= monthStart && e.Date < nextMonthStart)
            .SumAsync(e => (decimal?)e.Amount) ?? 0m;

        var byCategory = (await expenses
                .GroupBy(e => new { e.CategoryId, e.Category!.Name, e.Category.Color })
                .Select(g => new { g.Key.CategoryId, g.Key.Name, g.Key.Color, Total = g.Sum(x => x.Amount) })
                .ToListAsync())
            .OrderByDescending(x => x.Total)
            .Select(x => new CategoryTotal(x.CategoryId, x.Name, x.Color, x.Total))
            .ToList();

        var monthly = await expenses
            .Where(e => e.Date >= trendStart && e.Date < nextMonthStart)
            .GroupBy(e => new { e.Date.Year, e.Date.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(x => x.Amount) })
            .ToListAsync();

        // Months with no expenses still appear, with a total of 0
        var months = Enumerable.Range(0, 6)
            .Select(i => trendStart.AddMonths(i))
            .Select(m => new MonthTotal(
                m.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                monthly.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month)?.Total ?? 0m))
            .ToList();

        return new ExpenseSummary(totalSpent, count, thisMonth, byCategory, months);
    }

    public async Task<ExpenseResponse> GetByIdAsync(int id)
    {
        var expense = await MyExpenses()
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(ToResponse)
            .FirstOrDefaultAsync();

        return expense ?? throw new NotFoundException("Expense not found.");
    }

    public async Task<ExpenseResponse> CreateAsync(ExpenseRequest request)
    {
        ValidateRequest(request);
        await EnsureCategoryExistsAsync(request.CategoryId);

        var expense = new Expense
        {
            UserId = _user.Id,
            Amount = request.Amount,
            CategoryId = request.CategoryId,
            Description = CleanText(request.Description),
            Date = request.Date.Date
        };

        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync();

        return await GetByIdAsync(expense.Id);
    }

    public async Task UpdateAsync(int id, ExpenseRequest request)
    {
        var expense = await MyExpenses().FirstOrDefaultAsync(e => e.Id == id)
            ?? throw new NotFoundException("Expense not found.");

        ValidateRequest(request);
        await EnsureCategoryExistsAsync(request.CategoryId);

        expense.Amount = request.Amount;
        expense.CategoryId = request.CategoryId;
        expense.Description = CleanText(request.Description);
        expense.Date = request.Date.Date;

        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var expense = await MyExpenses().FirstOrDefaultAsync(e => e.Id == id)
            ?? throw new NotFoundException("Expense not found.");

        _db.Expenses.Remove(expense);
        await _db.SaveChangesAsync();
    }

    /* ---------- Business rules ---------- */

    private static void ValidateRequest(ExpenseRequest request)
    {
        var date = request.Date.Date;
        if (date < new DateTime(2000, 1, 1) || date > DateTime.Today.AddYears(1))
            throw new BadRequestException("Date must be between 2000 and one year from today.");
    }

    // The category must exist AND belong to this user
    private async Task EnsureCategoryExistsAsync(int categoryId)
    {
        var userId = _user.Id;
        if (!await _db.Categories.AnyAsync(c => c.Id == categoryId && c.UserId == userId))
            throw new BadRequestException("Category does not exist.");
    }

    // Trims the text, and turns empty text into null
    private static string? CleanText(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}