using ExpenseTracker.Api.Common;
using ExpenseTracker.Api.Models;

namespace ExpenseTracker.Tests.Unit;

public class ExpenseQueryTests
{
    // Four expenses used by most tests below
    private static IQueryable<Expense> Sample()
    {
        var food = new Category { Id = 1, Name = "Food" };
        var rent = new Category { Id = 2, Name = "Rent" };

        return new List<Expense>
        {
            new() { Id = 1, Amount = 100,   CategoryId = 1, Category = food, Date = new DateTime(2026, 10, 1) },
            new() { Id = 2, Amount = 500,   CategoryId = 1, Category = food, Date = new DateTime(2026, 10, 15) },
            new() { Id = 3, Amount = 12000, CategoryId = 2, Category = rent, Date = new DateTime(2026, 10, 31, 18, 30, 0) },
            new() { Id = 4, Amount = 250,   CategoryId = 2, Category = rent, Date = new DateTime(2026, 11, 1) }
        }.AsQueryable();
    }

    // Turns a result into "1,2,3" so the expected order is easy to read
    private static string Ids(IQueryable<Expense> query) => string.Join(",", query.Select(e => e.Id));

    /* ---------- Validation ---------- */

    [Fact]
    public void ThrowIfInvalid_RejectsAFromDateAfterTheToDate()
    {
        var query = new ExpenseQuery { From = new DateTime(2026, 10, 31), To = new DateTime(2026, 10, 1) };

        var ex = Assert.Throws<BadRequestException>(() => query.ThrowIfInvalid());

        Assert.Contains("From", ex.Message);
    }

    [Fact]
    public void ThrowIfInvalid_RejectsAMinimumAboveTheMaximum()
    {
        var query = new ExpenseQuery { MinAmount = 500, MaxAmount = 100 };

        var ex = Assert.Throws<BadRequestException>(() => query.ThrowIfInvalid());

        Assert.Contains("Minimum", ex.Message);
    }

    [Fact]
    public void ThrowIfInvalid_AllowsASingleDayAndAnExactAmount()
    {
        var day = new DateTime(2026, 10, 5);

        new ExpenseQuery { From = day, To = day, MinAmount = 5, MaxAmount = 5 }.ThrowIfInvalid();   // no exception means it passed
    }

    /* ---------- Filters ---------- */

    [Fact]
    public void ApplyFilters_WithNoFilters_ReturnsEverything()
    {
        Assert.Equal("1,2,3,4", Ids(Sample().ApplyFilters(new ExpenseQuery())));
    }

    [Fact]
    public void ApplyFilters_ByCategory()
    {
        var result = Sample().ApplyFilters(new ExpenseQuery { CategoryId = 1 });

        Assert.Equal("1,2", Ids(result));
    }

    [Fact]
    public void ApplyFilters_FromDateIsInclusive()
    {
        var result = Sample().ApplyFilters(new ExpenseQuery { From = new DateTime(2026, 10, 15) });

        Assert.Equal("2,3,4", Ids(result));
    }

    [Fact]
    public void ApplyFilters_ToDateIncludesTheWholeDay()
    {
        // Expense 3 is at 18:30 on 31 October, so it must still be included
        var result = Sample().ApplyFilters(new ExpenseQuery { To = new DateTime(2026, 10, 31) });

        Assert.Equal("1,2,3", Ids(result));
    }

    [Fact]
    public void ApplyFilters_AmountRangeIncludesBothEnds()
    {
        var result = Sample().ApplyFilters(new ExpenseQuery { MinAmount = 250, MaxAmount = 500 });

        Assert.Equal("2,4", Ids(result));
    }

    [Fact]
    public void ApplyFilters_CombinesAllTheFilters()
    {
        var result = Sample().ApplyFilters(new ExpenseQuery { CategoryId = 2, To = new DateTime(2026, 10, 31) });

        Assert.Equal("3", Ids(result));
    }

    /* ---------- Sorting ---------- */

    [Theory]
    [InlineData("date_desc", "4,3,2,1")]
    [InlineData(null, "4,3,2,1")]                 // no sort given: newest first
    [InlineData("something_unknown", "4,3,2,1")]  // an unknown sort falls back to newest first
    [InlineData("date_asc", "1,2,3,4")]
    [InlineData("amount_desc", "3,2,4,1")]
    [InlineData("amount_asc", "1,4,2,3")]
    [InlineData("category_asc", "2,1,4,3")]       // Food first, newest first inside each category
    public void ApplySort_OrdersAsRequested(string? sort, string expectedIds)
    {
        Assert.Equal(expectedIds, Ids(Sample().ApplySort(sort)));
    }
}