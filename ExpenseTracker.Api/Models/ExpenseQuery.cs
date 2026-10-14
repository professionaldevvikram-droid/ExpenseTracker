namespace ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Common;

// Filters the browser can send: /api/expenses?categoryId=2&minAmount=100&sort=amount_desc
public class ExpenseQuery
{
    public int? CategoryId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public string? Search { get; set; }
    public string? Sort { get; set; }      // date_desc (default), date_asc, amount_desc, amount_asc, category_asc
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public static class ExpenseQueryExtensions
{
    public static IQueryable<Expense> ApplyFilters(this IQueryable<Expense> query, ExpenseQuery f)
    {
        if (f.CategoryId.HasValue)
        {
            var categoryId = f.CategoryId.Value;
            query = query.Where(e => e.CategoryId == categoryId);
        }

        if (f.From.HasValue)
        {
            var start = f.From.Value.Date;
            query = query.Where(e => e.Date >= start);
        }

        if (f.To.HasValue)
        {
            var endExclusive = f.To.Value.Date.AddDays(1);   // include the whole "to" day
            query = query.Where(e => e.Date < endExclusive);
        }

        if (f.MinAmount.HasValue)
        {
            var min = f.MinAmount.Value;
            query = query.Where(e => e.Amount >= min);
        }

        if (f.MaxAmount.HasValue)
        {
            var max = f.MaxAmount.Value;
            query = query.Where(e => e.Amount <= max);
        }

        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var term = f.Search.Trim();
            query = query.Where(e =>
                e.Category!.Name.Contains(term) ||
                (e.Description != null && e.Description.Contains(term)));
        }

        return query;
    }

    public static IQueryable<Expense> ApplySort(this IQueryable<Expense> query, string? sort) => sort switch
    {
        "date_asc" => query.OrderBy(e => e.Date).ThenBy(e => e.Id),
        "amount_desc" => query.OrderByDescending(e => e.Amount).ThenByDescending(e => e.Id),
        "amount_asc" => query.OrderBy(e => e.Amount).ThenBy(e => e.Id),
        "category_asc" => query.OrderBy(e => e.Category!.Name).ThenByDescending(e => e.Date),
        _ => query.OrderByDescending(e => e.Date).ThenByDescending(e => e.Id)
    };

    public static void ThrowIfInvalid(this ExpenseQuery f)
    {
        if (f.From.HasValue && f.To.HasValue && f.From.Value.Date > f.To.Value.Date)
            throw new BadRequestException("'From' date must be before 'To' date.");

        if (f.MinAmount.HasValue && f.MaxAmount.HasValue && f.MinAmount > f.MaxAmount)
            throw new BadRequestException("Minimum amount can't be higher than maximum amount.");
    }
}