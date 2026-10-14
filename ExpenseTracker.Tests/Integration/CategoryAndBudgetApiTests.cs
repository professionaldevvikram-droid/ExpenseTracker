using System.Net;
using System.Net.Http.Json;
using ExpenseTracker.Api.Common;
using ExpenseTracker.Api.Models;

namespace ExpenseTracker.Tests.Integration;

[Collection("api")]
public class CategoryAndBudgetApiTests
{
    private readonly ApiFactory _factory;

    public CategoryAndBudgetApiTests(ApiFactory factory) => _factory = factory;

    private static CategoryRequest Category(string name, string color = "#4f46e5", decimal? budget = null) =>
        new() { Name = name, Color = color, MonthlyBudget = budget };

    /* ---------- Categories ---------- */

    [Fact]
    public async Task Create_AddsACategoryWithNoExpenses()
    {
        var user = await _factory.RegisterUserAsync();

        var response = await user.Client.PostAsJsonAsync("/api/categories", Category("Groceries", "#00aa55", 4000));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<CategoryResponse>())!;
        Assert.Equal("Groceries", created.Name);
        Assert.Equal(4000, created.MonthlyBudget);
        Assert.Equal(0, created.ExpenseCount);

        var all = await user.Client.GetCategoriesAsync();
        Assert.Equal(DefaultCategories.All.Length + 1, all.Count);
        Assert.Equal(all.Select(c => c.Name).Order(StringComparer.OrdinalIgnoreCase), all.Select(c => c.Name));   // sorted by name
    }

    [Fact]
    public async Task Create_RejectsADuplicateName_EvenInADifferentCase()
    {
        var user = await _factory.RegisterUserAsync();

        var response = await user.Client.PostAsJsonAsync("/api/categories", Category("food"));     // "Food" already exists

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("A category with this name already exists.", await response.MessageAsync());
    }

    [Fact]
    public async Task Create_RejectsABadColorOrAnEmptyName()
    {
        var user = await _factory.RegisterUserAsync();

        var color = await user.Client.PostAsJsonAsync("/api/categories", Category("Bad color", color: "red"));
        Assert.Equal(HttpStatusCode.BadRequest, color.StatusCode);
        Assert.Equal("Color must look like #4f46e5.", await color.MessageAsync());

        var name = await user.Client.PostAsJsonAsync("/api/categories", Category(""));
        Assert.Equal(HttpStatusCode.BadRequest, name.StatusCode);
    }

    [Fact]
    public async Task Update_ChangesTheNameColorAndBudget_AndKeepingTheSameNameIsFine()
    {
        var user = await _factory.RegisterUserAsync();
        var food = await user.Client.CategoryNamedAsync("Food");

        // Only the color and budget change, so the name is "taken" only by this same category
        var sameName = await user.Client.PutAsJsonAsync($"/api/categories/{food.Id}", Category("Food", "#00ff00", 1500));
        Assert.Equal(HttpStatusCode.NoContent, sameName.StatusCode);

        var renamed = await user.Client.PutAsJsonAsync($"/api/categories/{food.Id}", Category("Eating out", "#ff0000", 1500));
        Assert.Equal(HttpStatusCode.NoContent, renamed.StatusCode);

        var saved = (await user.Client.GetFromJsonAsync<CategoryResponse>($"/api/categories/{food.Id}"))!;
        Assert.Equal("Eating out", saved.Name);
        Assert.Equal("#ff0000", saved.Color);
        Assert.Equal(1500, saved.MonthlyBudget);
    }

    [Fact]
    public async Task Update_RejectsANameAnotherCategoryUses()
    {
        var user = await _factory.RegisterUserAsync();
        var transport = await user.Client.CategoryNamedAsync("Transport");

        var response = await user.Client.PutAsJsonAsync($"/api/categories/{transport.Id}", Category("Food"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Delete_IsRefusedWhileExpensesUseTheCategory_ThenAllowedWhenTheyAreGone()
    {
        var user = await _factory.RegisterUserAsync();
        var food = await user.Client.CategoryNamedAsync("Food");
        var expense = await user.Client.AddExpenseAsync(food.Id, 100);

        var blocked = await user.Client.DeleteAsync($"/api/categories/{food.Id}");
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Contains("can't be deleted", await blocked.MessageAsync());
        Assert.Equal(1, (await user.Client.CategoryNamedAsync("Food")).ExpenseCount);

        (await user.Client.DeleteAsync($"/api/expenses/{expense.Id}")).EnsureSuccessStatusCode();

        var allowed = await user.Client.DeleteAsync($"/api/categories/{food.Id}");
        Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
        Assert.DoesNotContain(await user.Client.GetCategoriesAsync(), c => c.Id == food.Id);
    }

    [Fact]
    public async Task Category_DoesNotExist_IsNotFound()
    {
        var user = await _factory.RegisterUserAsync();

        var response = await user.Client.GetAsync("/api/categories/999999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Category not found.", await response.MessageAsync());
    }

    /* ---------- Budgets ---------- */

    [Fact]
    public async Task BudgetStatus_AddsUpTheChosenMonth_ForCategoriesWithABudget()
    {
        var user = await _factory.RegisterUserAsync();
        var food = await user.Client.CategoryNamedAsync("Food");
        var rent = await user.Client.CategoryNamedAsync("Rent");

        (await user.Client.PutAsJsonAsync($"/api/categories/{food.Id}", Category("Food", food.Color, 1000)))
            .EnsureSuccessStatusCode();

        await user.Client.AddExpenseAsync(food.Id, 300, new DateTime(2025, 3, 5));
        await user.Client.AddExpenseAsync(food.Id, 200, new DateTime(2025, 3, 31));    // the last day of the month counts
        await user.Client.AddExpenseAsync(food.Id, 999, new DateTime(2025, 2, 28));    // the month before
        await user.Client.AddExpenseAsync(food.Id, 888, new DateTime(2025, 4, 1));     // the month after
        await user.Client.AddExpenseAsync(rent.Id, 12000, new DateTime(2025, 3, 1));   // no budget, so not listed

        var status = (await user.Client.GetFromJsonAsync<List<BudgetStatus>>("/api/budgets/status?month=2025-03"))!;

        var only = Assert.Single(status);
        Assert.Equal("Food", only.Name);
        Assert.Equal(1000, only.Budget);
        Assert.Equal(500, only.Spent);
    }

    [Fact]
    public async Task BudgetStatus_UsesTheCurrentMonthByDefault()
    {
        var user = await _factory.RegisterUserAsync();
        var food = await user.Client.CategoryNamedAsync("Food");
        (await user.Client.PutAsJsonAsync($"/api/categories/{food.Id}", Category("Food", food.Color, 800)))
            .EnsureSuccessStatusCode();
        await user.Client.AddExpenseAsync(food.Id, 250, DateTime.Today);

        var status = (await user.Client.GetFromJsonAsync<List<BudgetStatus>>("/api/budgets/status"))!;

        Assert.Equal(250, Assert.Single(status).Spent);
    }

    [Fact]
    public async Task BudgetOfZero_RemovesTheBudget()
    {
        var user = await _factory.RegisterUserAsync();
        var food = await user.Client.CategoryNamedAsync("Food");
        await user.Client.PutAsJsonAsync($"/api/categories/{food.Id}", Category("Food", food.Color, 800));
        Assert.Single((await user.Client.GetFromJsonAsync<List<BudgetStatus>>("/api/budgets/status"))!);

        await user.Client.PutAsJsonAsync($"/api/categories/{food.Id}", Category("Food", food.Color, 0));

        Assert.Empty((await user.Client.GetFromJsonAsync<List<BudgetStatus>>("/api/budgets/status"))!);
        Assert.Null((await user.Client.CategoryNamedAsync("Food")).MonthlyBudget);
    }

    [Theory]
    [InlineData("2026-13")]
    [InlineData("October")]
    [InlineData("2026/10")]
    public async Task BudgetStatus_RejectsAMonthInTheWrongFormat(string month)
    {
        var user = await _factory.RegisterUserAsync();

        var response = await user.Client.GetAsync($"/api/budgets/status?month={Uri.EscapeDataString(month)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Month", await response.MessageAsync());
    }
}