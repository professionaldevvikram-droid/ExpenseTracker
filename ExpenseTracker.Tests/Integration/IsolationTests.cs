using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using ExpenseTracker.Api.Models;

namespace ExpenseTracker.Tests.Integration;

// The most important tests in the project: nobody can see or change anybody else's data.
[Collection("api")]
public class IsolationTests
{
    private readonly ApiFactory _factory;

    public IsolationTests(ApiFactory factory) => _factory = factory;

    private sealed record World(
        TestUser Alice, TestUser Bob,
        CategoryResponse AliceFood, CategoryResponse BobFood,
        ExpenseResponse AliceExpense, ExpenseResponse BobExpense);

    // Two users, each with one expense in their own "Food" category
    private async Task<World> CreateWorldAsync()
    {
        var alice = await _factory.RegisterUserAsync("Alice");
        var bob = await _factory.RegisterUserAsync("Bob");

        var aliceFood = await alice.Client.CategoryNamedAsync("Food");
        var bobFood = await bob.Client.CategoryNamedAsync("Food");

        var aliceExpense = await alice.Client.AddExpenseAsync(aliceFood.Id, 100, DateTime.Today, "Alice secret");
        var bobExpense = await bob.Client.AddExpenseAsync(bobFood.Id, 7, DateTime.Today, "Bob lunch");

        return new World(alice, bob, aliceFood, bobFood, aliceExpense, bobExpense);
    }

    [Fact]
    public async Task EachUserOnlySeesTheirOwnExpenses()
    {
        var w = await CreateWorldAsync();

        var alices = await w.Alice.Client.ListExpensesAsync();
        var bobs = await w.Bob.Client.ListExpensesAsync();

        Assert.Equal(w.AliceExpense.Id, Assert.Single(alices.Items).Id);
        Assert.Equal(100, alices.TotalAmount);
        Assert.Equal(w.BobExpense.Id, Assert.Single(bobs.Items).Id);
        Assert.Equal(7, bobs.TotalAmount);
    }

    [Fact]
    public async Task AnotherUsersExpense_IsNotFound_ForReadUpdateAndDelete_AndStaysUnchanged()
    {
        var w = await CreateWorldAsync();
        var url = $"/api/expenses/{w.AliceExpense.Id}";

        Assert.Equal(HttpStatusCode.NotFound, (await w.Bob.Client.GetAsync(url)).StatusCode);

        var update = await w.Bob.Client.PutAsJsonAsync(url,
            new ExpenseRequest { Amount = 1, CategoryId = w.BobFood.Id, Date = DateTime.Today, Description = "Hacked" });
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await w.Bob.Client.DeleteAsync(url)).StatusCode);

        // Alice's expense is exactly as she left it
        var stillThere = (await w.Alice.Client.GetFromJsonAsync<ExpenseResponse>(url))!;
        Assert.Equal(100, stillThere.Amount);
        Assert.Equal("Alice secret", stillThere.Description);
    }

    [Fact]
    public async Task CannotUseSomeoneElsesCategory_WhenCreatingOrEditing()
    {
        var w = await CreateWorldAsync();

        var create = await w.Bob.Client.PostAsJsonAsync("/api/expenses",
            new ExpenseRequest { Amount = 5, CategoryId = w.AliceFood.Id, Date = DateTime.Today });
        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
        Assert.Equal("Category does not exist.", await create.MessageAsync());

        var move = await w.Bob.Client.PutAsJsonAsync($"/api/expenses/{w.BobExpense.Id}",
            new ExpenseRequest { Amount = 7, CategoryId = w.AliceFood.Id, Date = DateTime.Today });
        Assert.Equal(HttpStatusCode.BadRequest, move.StatusCode);

        var bobsExpense = (await w.Bob.Client.GetFromJsonAsync<ExpenseResponse>($"/api/expenses/{w.BobExpense.Id}"))!;
        Assert.Equal(w.BobFood.Id, bobsExpense.CategoryId);
    }

    [Fact]
    public async Task AnotherUsersCategory_IsNotFound_ForReadUpdateAndDelete()
    {
        var w = await CreateWorldAsync();
        var url = $"/api/categories/{w.AliceFood.Id}";

        Assert.DoesNotContain(await w.Bob.Client.GetCategoriesAsync(), c => c.Id == w.AliceFood.Id);

        Assert.Equal(HttpStatusCode.NotFound, (await w.Bob.Client.GetAsync(url)).StatusCode);

        var update = await w.Bob.Client.PutAsJsonAsync(url,
            new CategoryRequest { Name = "Hacked", Color = "#ff0000" });
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await w.Bob.Client.DeleteAsync(url)).StatusCode);

        var stillThere = (await w.Alice.Client.GetFromJsonAsync<CategoryResponse>(url))!;
        Assert.Equal("Food", stillThere.Name);
    }

    [Fact]
    public async Task SearchNeverReturnsSomeoneElsesExpenses()
    {
        var w = await CreateWorldAsync();

        var bobSearching = await w.Bob.Client.ListExpensesAsync("?search=secret");
        var aliceSearching = await w.Alice.Client.ListExpensesAsync("?search=secret");

        Assert.Equal(0, bobSearching.TotalCount);
        Assert.Equal(1, aliceSearching.TotalCount);
    }

    [Fact]
    public async Task SummariesAndBudgetsAreSeparate()
    {
        var w = await CreateWorldAsync();
        (await w.Alice.Client.PutAsJsonAsync($"/api/categories/{w.AliceFood.Id}",
            new CategoryRequest { Name = "Food", Color = w.AliceFood.Color, MonthlyBudget = 500 })).EnsureSuccessStatusCode();

        var alice = (await w.Alice.Client.GetFromJsonAsync<ExpenseSummary>("/api/expenses/summary"))!;
        var bob = (await w.Bob.Client.GetFromJsonAsync<ExpenseSummary>("/api/expenses/summary"))!;
        Assert.Equal(100, alice.TotalSpent);
        Assert.Equal(1, alice.Count);
        Assert.Equal(7, bob.TotalSpent);
        Assert.Equal(1, bob.Count);

        var alicesBudgets = (await w.Alice.Client.GetFromJsonAsync<List<BudgetStatus>>("/api/budgets/status"))!;
        var bobsBudgets = (await w.Bob.Client.GetFromJsonAsync<List<BudgetStatus>>("/api/budgets/status"))!;
        Assert.Equal(100, Assert.Single(alicesBudgets).Spent);
        Assert.Empty(bobsBudgets);
    }

    [Fact]
    public async Task ExportContainsOnlyTheCallersExpenses()
    {
        var w = await CreateWorldAsync();

        var response = await w.Bob.Client.GetAsync("/api/expenses/export");

        using var workbook = new XLWorkbook(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
        var sheet = workbook.Worksheet("Expenses");
        Assert.Equal("Bob lunch", sheet.Cell(2, 3).GetString());
        Assert.Equal("Total", sheet.Cell(3, 3).GetString());
        Assert.Equal(7, sheet.Cell(3, 4).GetDouble());                  // Alice's 100 is not in Bob's file
    }

    [Fact]
    public async Task DifferentUsers_CanUseTheSameCategoryName_ButGetDifferentIds()
    {
        var w = await CreateWorldAsync();

        Assert.NotEqual(w.AliceFood.Id, w.BobFood.Id);

        var alice = await w.Alice.Client.PostAsJsonAsync("/api/categories", new CategoryRequest { Name = "Groceries", Color = "#00aa55" });
        var bob = await w.Bob.Client.PostAsJsonAsync("/api/categories", new CategoryRequest { Name = "Groceries", Color = "#00aa55" });

        Assert.Equal(HttpStatusCode.Created, alice.StatusCode);
        Assert.Equal(HttpStatusCode.Created, bob.StatusCode);
    }
}