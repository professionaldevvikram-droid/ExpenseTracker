using System.Globalization;
using ExpenseTracker.Api.Common;
using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Services;
using ExpenseTracker.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Tests.Unit;

public class ExpenseServiceTests
{
    private const int Alice = 1;
    private const int Bob = 2;

    private static ExpenseService ServiceFor(AppDbContext db, int userId) =>
        new(db, new FakeCurrentUser(userId));

    private static ExpenseRequest Request(
        int categoryId, decimal amount = 100, DateTime? date = null, string? description = null) =>
        new() { Amount = amount, CategoryId = categoryId, Date = date ?? DateTime.Today, Description = description };

    /* ---------- Creating ---------- */

    [Fact]
    public async Task Create_SavesTheExpenseForTheCurrentUser()
    {
        // Arrange: Alice has a Food category
        using var db = TestDb.Create();
        var food = TestDb.AddCategory(db, Alice, "Food");
        var service = ServiceFor(db, Alice);

        // Act
        var created = await service.CreateAsync(Request(food.Id, 250, description: "Lunch"));

        // Assert: the response is right, and the row in the database belongs to Alice
        Assert.Equal(250, created.Amount);
        Assert.Equal("Food", created.Category);

        var saved = await db.Expenses.SingleAsync();
        Assert.Equal(Alice, saved.UserId);
    }

    [Theory]
    [InlineData("  Lunch  ", "Lunch")]   // spaces around the text are removed
    [InlineData("   ", null)]            // only spaces becomes "no description"
    [InlineData(null, null)]
    public async Task Create_CleansUpTheDescription(string? input, string? expected)
    {
        using var db = TestDb.Create();
        var food = TestDb.AddCategory(db, Alice, "Food");

        await ServiceFor(db, Alice).CreateAsync(Request(food.Id, description: input));

        Assert.Equal(expected, (await db.Expenses.SingleAsync()).Description);
    }

    [Fact]
    public async Task Create_RejectsADateBeforeTheYear2000()
    {
        using var db = TestDb.Create();
        var food = TestDb.AddCategory(db, Alice, "Food");

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            ServiceFor(db, Alice).CreateAsync(Request(food.Id, date: new DateTime(1999, 12, 31))));

        Assert.Contains("Date", ex.Message);
        Assert.Empty(db.Expenses);                       // nothing was saved
    }

    [Fact]
    public async Task Create_RejectsADateMoreThanAYearAhead()
    {
        using var db = TestDb.Create();
        var food = TestDb.AddCategory(db, Alice, "Food");

        await Assert.ThrowsAsync<BadRequestException>(() =>
            ServiceFor(db, Alice).CreateAsync(Request(food.Id, date: DateTime.Today.AddYears(1).AddDays(1))));
    }

    [Fact]
    public async Task Create_AcceptsADateExactlyOneYearAhead()
    {
        using var db = TestDb.Create();
        var food = TestDb.AddCategory(db, Alice, "Food");

        await ServiceFor(db, Alice).CreateAsync(Request(food.Id, date: DateTime.Today.AddYears(1)));

        Assert.Single(db.Expenses);
    }

    [Fact]
    public async Task Create_RejectsACategoryThatDoesNotExist()
    {
        using var db = TestDb.Create();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            ServiceFor(db, Alice).CreateAsync(Request(categoryId: 999)));

        Assert.Equal("Category does not exist.", ex.Message);
    }

    [Fact]
    public async Task Create_RejectsAnotherUsersCategory()
    {
        using var db = TestDb.Create();
        var bobsCategory = TestDb.AddCategory(db, Bob, "Food");

        await Assert.ThrowsAsync<BadRequestException>(() =>
            ServiceFor(db, Alice).CreateAsync(Request(bobsCategory.Id)));

        Assert.Empty(db.Expenses);
    }

    /* ---------- Reading ---------- */

    [Fact]
    public async Task GetById_ReturnsMyExpense_ButNotSomeoneElses()
    {
        using var db = TestDb.Create();
        var alicesFood = TestDb.AddCategory(db, Alice, "Food");
        var alicesExpense = TestDb.AddExpense(db, Alice, alicesFood.Id, 100, DateTime.Today);

        var mine = await ServiceFor(db, Alice).GetByIdAsync(alicesExpense.Id);
        Assert.Equal(alicesExpense.Id, mine.Id);

        // Bob gets "not found", exactly as if the expense didn't exist
        await Assert.ThrowsAsync<NotFoundException>(() => ServiceFor(db, Bob).GetByIdAsync(alicesExpense.Id));
    }

    [Fact]
    public async Task GetPaged_OnlyReturnsMyExpenses_WithTotals()
    {
        using var db = TestDb.Create();
        var alicesFood = TestDb.AddCategory(db, Alice, "Food");
        var bobsFood = TestDb.AddCategory(db, Bob, "Food");

        TestDb.AddExpense(db, Alice, alicesFood.Id, 100, DateTime.Today);
        TestDb.AddExpense(db, Alice, alicesFood.Id, 200, DateTime.Today);
        TestDb.AddExpense(db, Alice, alicesFood.Id, 300, DateTime.Today);
        TestDb.AddExpense(db, Bob, bobsFood.Id, 1000, DateTime.Today);

        var result = await ServiceFor(db, Alice).GetPagedAsync(new ExpenseQuery());

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(600, result.TotalAmount);
        Assert.Equal(3, result.Items.Count);
    }

    [Fact]
    public async Task GetPaged_SplitsTheResultsIntoPages()
    {
        using var db = TestDb.Create();
        var food = TestDb.AddCategory(db, Alice, "Food");
        for (var i = 0; i < 25; i++)
            TestDb.AddExpense(db, Alice, food.Id, 10, DateTime.Today);

        var service = ServiceFor(db, Alice);

        var first = await service.GetPagedAsync(new ExpenseQuery { Page = 1, PageSize = 10 });
        var last = await service.GetPagedAsync(new ExpenseQuery { Page = 3, PageSize = 10 });

        Assert.Equal(10, first.Items.Count);
        Assert.Equal(3, first.TotalPages);
        Assert.Equal(25, first.TotalCount);
        Assert.Equal(250, first.TotalAmount);            // the total covers all pages, not just this one
        Assert.Equal(5, last.Items.Count);
    }

    [Fact]
    public async Task GetPaged_MovesAnOutOfRangePageToTheNearestValidOne()
    {
        using var db = TestDb.Create();
        var food = TestDb.AddCategory(db, Alice, "Food");
        for (var i = 0; i < 25; i++)
            TestDb.AddExpense(db, Alice, food.Id, 10, DateTime.Today);

        var result = await ServiceFor(db, Alice).GetPagedAsync(new ExpenseQuery { Page = 99, PageSize = 10 });

        Assert.Equal(3, result.Page);
        Assert.Equal(5, result.Items.Count);
    }

    [Fact]
    public async Task GetPaged_LimitsThePageSizeTo100()
    {
        using var db = TestDb.Create();

        var result = await ServiceFor(db, Alice).GetPagedAsync(new ExpenseQuery { PageSize = 1000 });

        Assert.Equal(100, result.PageSize);
    }

    [Fact]
    public async Task GetPaged_RejectsAnInvalidDateRange()
    {
        using var db = TestDb.Create();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            ServiceFor(db, Alice).GetPagedAsync(new ExpenseQuery
            {
                From = new DateTime(2026, 10, 31),
                To = new DateTime(2026, 10, 1)
            }));
    }

    [Fact]
    public async Task GetSummary_CountsOnlyMyExpenses()
    {
        using var db = TestDb.Create();
        var today = DateTime.Today;

        var food = TestDb.AddCategory(db, Alice, "Food");
        var rent = TestDb.AddCategory(db, Alice, "Rent");
        var bobsFood = TestDb.AddCategory(db, Bob, "Food");

        TestDb.AddExpense(db, Alice, food.Id, 300, today);                       // this month
        TestDb.AddExpense(db, Alice, rent.Id, 12000, today.AddMonths(-2));       // two months ago
        TestDb.AddExpense(db, Bob, bobsFood.Id, 999, today);                     // not Alice's

        var summary = await ServiceFor(db, Alice).GetSummaryAsync();

        Assert.Equal(12300, summary.TotalSpent);
        Assert.Equal(2, summary.Count);
        Assert.Equal(300, summary.ThisMonth);
        Assert.Equal("Rent", summary.ByCategory[0].Name);                         // biggest category first

        Assert.Equal(6, summary.Months.Count);                                    // always six months, even empty ones
        Assert.Equal(today.ToString("yyyy-MM", CultureInfo.InvariantCulture), summary.Months[^1].Month);
        Assert.Equal(12300, summary.Months.Sum(m => m.Total));
    }

    /* ---------- Updating and deleting ---------- */

    [Fact]
    public async Task Update_ChangesTheExpense()
    {
        using var db = TestDb.Create();
        var food = TestDb.AddCategory(db, Alice, "Food");
        var transport = TestDb.AddCategory(db, Alice, "Transport");
        var expense = TestDb.AddExpense(db, Alice, food.Id, 100, DateTime.Today, "Old");

        await ServiceFor(db, Alice).UpdateAsync(expense.Id, Request(transport.Id, 450, description: "New"));

        var saved = await db.Expenses.SingleAsync();
        Assert.Equal(450, saved.Amount);
        Assert.Equal(transport.Id, saved.CategoryId);
        Assert.Equal("New", saved.Description);
    }

    [Fact]
    public async Task Update_CannotTouchAnotherUsersExpense()
    {
        using var db = TestDb.Create();
        var alicesFood = TestDb.AddCategory(db, Alice, "Food");
        var bobsFood = TestDb.AddCategory(db, Bob, "Food");
        var alicesExpense = TestDb.AddExpense(db, Alice, alicesFood.Id, 100, DateTime.Today);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ServiceFor(db, Bob).UpdateAsync(alicesExpense.Id, Request(bobsFood.Id, 1)));

        Assert.Equal(100, (await db.Expenses.SingleAsync()).Amount);              // still unchanged
    }

    [Fact]
    public async Task Update_CannotMoveAnExpenseIntoAnotherUsersCategory()
    {
        using var db = TestDb.Create();
        var alicesFood = TestDb.AddCategory(db, Alice, "Food");
        var bobsFood = TestDb.AddCategory(db, Bob, "Food");
        var expense = TestDb.AddExpense(db, Alice, alicesFood.Id, 100, DateTime.Today);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            ServiceFor(db, Alice).UpdateAsync(expense.Id, Request(bobsFood.Id)));

        Assert.Equal(alicesFood.Id, (await db.Expenses.SingleAsync()).CategoryId);
    }

    [Fact]
    public async Task Delete_RemovesMyExpense()
    {
        using var db = TestDb.Create();
        var food = TestDb.AddCategory(db, Alice, "Food");
        var expense = TestDb.AddExpense(db, Alice, food.Id, 100, DateTime.Today);

        await ServiceFor(db, Alice).DeleteAsync(expense.Id);

        Assert.Empty(db.Expenses);
    }

    [Fact]
    public async Task Delete_CannotRemoveAnotherUsersExpense()
    {
        using var db = TestDb.Create();
        var food = TestDb.AddCategory(db, Alice, "Food");
        var expense = TestDb.AddExpense(db, Alice, food.Id, 100, DateTime.Today);

        await Assert.ThrowsAsync<NotFoundException>(() => ServiceFor(db, Bob).DeleteAsync(expense.Id));

        Assert.Single(db.Expenses);                                               // it is still there
    }
}