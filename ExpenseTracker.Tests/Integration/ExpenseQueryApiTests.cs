using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using ExpenseTracker.Api.Models;

namespace ExpenseTracker.Tests.Integration;

[Collection("api")]
public class ExpenseQueryApiTests
{
    private const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly ApiFactory _factory;

    public ExpenseQueryApiTests(ApiFactory factory) => _factory = factory;

    // Four expenses in March and April 2025. Fixed dates, so these tests never depend on today's date.
    private async Task<TestUser> SeedAsync()
    {
        var user = await _factory.RegisterUserAsync();
        var food = await user.Client.CategoryNamedAsync("Food");
        var rent = await user.Client.CategoryNamedAsync("Rent");

        await user.Client.AddExpenseAsync(food.Id, 100, new DateTime(2025, 3, 1), "Morning coffee");
        await user.Client.AddExpenseAsync(food.Id, 500, new DateTime(2025, 3, 15), "Dinner out");
        await user.Client.AddExpenseAsync(rent.Id, 12000, new DateTime(2025, 3, 31), "March rent");
        await user.Client.AddExpenseAsync(rent.Id, 250, new DateTime(2025, 4, 1), "Late fee");

        return user;
    }

    private static string?[] Descriptions(PagedResult<ExpenseResponse> page) =>
        page.Items.Select(e => e.Description).ToArray();

    /* ---------- Search and filters (on real SQL Server) ---------- */

    [Fact]
    public async Task Search_IgnoresUpperAndLowerCase_AndMatchesDescriptionsAndCategoryNames()
    {
        var user = await SeedAsync();

        var byDescription = await user.Client.ListExpensesAsync("?search=COFFEE");
        Assert.Equal(new[] { "Morning coffee" }, Descriptions(byDescription));

        var byCategory = await user.Client.ListExpensesAsync("?search=food");
        Assert.Equal(2, byCategory.TotalCount);

        var nothing = await user.Client.ListExpensesAsync("?search=zebra");
        Assert.Equal(0, nothing.TotalCount);
    }

    [Fact]
    public async Task Filter_ByCategory()
    {
        var user = await SeedAsync();
        var rent = await user.Client.CategoryNamedAsync("Rent");

        var result = await user.Client.ListExpensesAsync($"?categoryId={rent.Id}");

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, e => Assert.Equal("Rent", e.Category));
    }

    [Fact]
    public async Task Filter_ByDates_IncludesBothEnds()
    {
        var user = await SeedAsync();

        var result = await user.Client.ListExpensesAsync("?from=2025-03-15&to=2025-03-31&sort=date_asc");

        Assert.Equal(new[] { "Dinner out", "March rent" }, Descriptions(result));
    }

    [Fact]
    public async Task Filter_ByAmount_IncludesBothEnds()
    {
        var user = await SeedAsync();

        var result = await user.Client.ListExpensesAsync("?minAmount=250&maxAmount=500&sort=amount_asc");

        Assert.Equal(new[] { "Late fee", "Dinner out" }, Descriptions(result));
        Assert.Equal(750, result.TotalAmount);
    }

    [Fact]
    public async Task Filters_CanBeCombined()
    {
        var user = await SeedAsync();
        var rent = await user.Client.CategoryNamedAsync("Rent");

        var result = await user.Client.ListExpensesAsync($"?categoryId={rent.Id}&to=2025-03-31");

        Assert.Equal(new[] { "March rent" }, Descriptions(result));
    }

    [Theory]
    [InlineData("date_desc", new[] { "Late fee", "March rent", "Dinner out", "Morning coffee" })]
    [InlineData("date_asc", new[] { "Morning coffee", "Dinner out", "March rent", "Late fee" })]
    [InlineData("amount_desc", new[] { "March rent", "Dinner out", "Late fee", "Morning coffee" })]
    [InlineData("amount_asc", new[] { "Morning coffee", "Late fee", "Dinner out", "March rent" })]
    [InlineData("category_asc", new[] { "Dinner out", "Morning coffee", "Late fee", "March rent" })]
    public async Task Sorting_OrdersTheList(string sort, string[] expected)
    {
        var user = await SeedAsync();

        var result = await user.Client.ListExpensesAsync($"?sort={sort}");

        Assert.Equal(expected, Descriptions(result));
    }

    [Fact]
    public async Task Sorting_DefaultsToNewestFirst()
    {
        var user = await SeedAsync();

        var result = await user.Client.ListExpensesAsync();

        Assert.Equal("Late fee", result.Items[0].Description);
    }

    [Fact]
    public async Task InvalidRanges_AreRejectedWithAMessage()
    {
        var user = await _factory.RegisterUserAsync();

        var dates = await user.Client.GetAsync("/api/expenses?from=2025-04-01&to=2025-03-01");
        Assert.Equal(HttpStatusCode.BadRequest, dates.StatusCode);
        Assert.Equal("'From' date must be before 'To' date.", await dates.MessageAsync());

        var amounts = await user.Client.GetAsync("/api/expenses?minAmount=500&maxAmount=100");
        Assert.Equal(HttpStatusCode.BadRequest, amounts.StatusCode);
        Assert.Contains("Minimum", await amounts.MessageAsync());
    }

    /* ---------- Paging ---------- */

    [Fact]
    public async Task Paging_SplitsTheResults_AndTotalsCoverEveryPage()
    {
        var user = await _factory.RegisterUserAsync();
        var food = await user.Client.CategoryNamedAsync("Food");
        for (var i = 0; i < 25; i++)
            await user.Client.AddExpenseAsync(food.Id, 10);

        var first = await user.Client.ListExpensesAsync("?page=1&pageSize=10");
        var last = await user.Client.ListExpensesAsync("?page=3&pageSize=10");

        Assert.Equal(10, first.Items.Count);
        Assert.Equal(3, first.TotalPages);
        Assert.Equal(25, first.TotalCount);
        Assert.Equal(250, first.TotalAmount);
        Assert.Equal(5, last.Items.Count);
    }

    [Fact]
    public async Task Paging_MovesAnOutOfRangePageToTheLastOne_AndCapsThePageSize()
    {
        var user = await _factory.RegisterUserAsync();
        var food = await user.Client.CategoryNamedAsync("Food");
        for (var i = 0; i < 25; i++)
            await user.Client.AddExpenseAsync(food.Id, 10);

        var beyond = await user.Client.ListExpensesAsync("?page=99&pageSize=10");
        Assert.Equal(3, beyond.Page);
        Assert.Equal(5, beyond.Items.Count);

        var huge = await user.Client.ListExpensesAsync("?pageSize=1000");
        Assert.Equal(100, huge.PageSize);
    }

    /* ---------- Summary ---------- */

    [Fact]
    public async Task Summary_AddsUpEverythingInSql()
    {
        var user = await _factory.RegisterUserAsync();
        var food = await user.Client.CategoryNamedAsync("Food");
        var rent = await user.Client.CategoryNamedAsync("Rent");
        var today = DateTime.Today;

        await user.Client.AddExpenseAsync(food.Id, 300, today);
        await user.Client.AddExpenseAsync(rent.Id, 12000, today.AddMonths(-2));

        var summary = (await user.Client.GetFromJsonAsync<ExpenseSummary>("/api/expenses/summary"))!;

        Assert.Equal(12300, summary.TotalSpent);
        Assert.Equal(2, summary.Count);
        Assert.Equal(300, summary.ThisMonth);
        Assert.Equal("Rent", summary.ByCategory[0].Name);                  // the biggest category comes first
        Assert.Equal(12000, summary.ByCategory[0].Total);

        Assert.Equal(6, summary.Months.Count);                              // six months, even the empty ones
        Assert.Equal(today.ToString("yyyy-MM", CultureInfo.InvariantCulture), summary.Months[^1].Month);
        Assert.Equal(300, summary.Months[^1].Total);
        Assert.Equal(12300, summary.Months.Sum(m => m.Total));
    }

    [Fact]
    public async Task Summary_ForANewUser_IsAllZeros()
    {
        var user = await _factory.RegisterUserAsync();

        var summary = (await user.Client.GetFromJsonAsync<ExpenseSummary>("/api/expenses/summary"))!;

        Assert.Equal(0, summary.TotalSpent);
        Assert.Equal(0, summary.Count);
        Assert.Empty(summary.ByCategory);
        Assert.Equal(6, summary.Months.Count);
    }

    /* ---------- Excel export ---------- */

    [Fact]
    public async Task Export_ProducesAWorkbookWithTheExpensesAndTotals()
    {
        var user = await _factory.RegisterUserAsync();
        var food = await user.Client.CategoryNamedAsync("Food");
        var rent = await user.Client.CategoryNamedAsync("Rent");
        await user.Client.AddExpenseAsync(food.Id, 200, new DateTime(2025, 3, 1), "Lunch");
        await user.Client.AddExpenseAsync(rent.Id, 500, new DateTime(2025, 3, 2), "Flat rent");

        var response = await user.Client.GetAsync("/api/expenses/export");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(XlsxType, response.Content.Headers.ContentType!.MediaType);
        Assert.Contains(".xlsx", response.Content.Headers.ContentDisposition!.FileName);

        using var workbook = new XLWorkbook(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));

        var expenses = workbook.Worksheet("Expenses");
        Assert.Equal("Date", expenses.Cell(1, 1).GetString());
        Assert.Equal("Food", expenses.Cell(2, 2).GetString());               // oldest first
        Assert.Equal("Lunch", expenses.Cell(2, 3).GetString());
        Assert.Equal(200, expenses.Cell(2, 4).GetDouble());
        Assert.Equal("Rent", expenses.Cell(3, 2).GetString());
        Assert.Equal("Total", expenses.Cell(4, 3).GetString());
        Assert.Equal(700, expenses.Cell(4, 4).GetDouble());

        var byCategory = workbook.Worksheet("By category");
        Assert.Equal("Rent", byCategory.Cell(2, 1).GetString());             // the biggest category first
        Assert.Equal(500, byCategory.Cell(2, 3).GetDouble());
        Assert.Equal(700, byCategory.Cell(4, 3).GetDouble());
    }

    [Fact]
    public async Task Export_OnlyIncludesTheChosenDateRange()
    {
        var user = await _factory.RegisterUserAsync();
        var food = await user.Client.CategoryNamedAsync("Food");
        await user.Client.AddExpenseAsync(food.Id, 200, new DateTime(2025, 3, 1));
        await user.Client.AddExpenseAsync(food.Id, 500, new DateTime(2025, 3, 31));
        await user.Client.AddExpenseAsync(food.Id, 50, new DateTime(2025, 4, 15));

        var response = await user.Client.GetAsync("/api/expenses/export?from=2025-03-01&to=2025-03-31");

        using var workbook = new XLWorkbook(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
        var sheet = workbook.Worksheet("Expenses");
        Assert.Equal("Total", sheet.Cell(4, 3).GetString());                 // two expenses, so the total is on row 4
        Assert.Equal(700, sheet.Cell(4, 4).GetDouble());                     // the 15 April expense is left out
    }

    [Fact]
    public async Task Export_WithNothingToExport_IsNotFound_AndABadRangeIsRejected()
    {
        var user = await _factory.RegisterUserAsync();

        var empty = await user.Client.GetAsync("/api/expenses/export");
        Assert.Equal(HttpStatusCode.NotFound, empty.StatusCode);
        Assert.Equal("No expenses found in this date range.", await empty.MessageAsync());

        var backwards = await user.Client.GetAsync("/api/expenses/export?from=2025-04-01&to=2025-03-01");
        Assert.Equal(HttpStatusCode.BadRequest, backwards.StatusCode);
    }
}