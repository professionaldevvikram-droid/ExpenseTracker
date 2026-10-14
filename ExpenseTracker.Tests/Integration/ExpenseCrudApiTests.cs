using System.Net;
using System.Net.Http.Json;
using ExpenseTracker.Api.Models;

namespace ExpenseTracker.Tests.Integration;

[Collection("api")]
public class ExpenseCrudApiTests
{
    private readonly ApiFactory _factory;

    public ExpenseCrudApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_ThenReadItBack()
    {
        var user = await _factory.RegisterUserAsync();
        var food = await user.Client.CategoryNamedAsync("Food");

        var response = await user.Client.PostAsJsonAsync("/api/expenses", new ExpenseRequest
        {
            Amount = 250.5m,
            CategoryId = food.Id,
            Date = new DateTime(2026, 3, 5),
            Description = "  Lunch  "
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<ExpenseResponse>())!;
        Assert.Equal("Lunch", created.Description);                         // spaces are trimmed
        Assert.Equal("Food", created.Category);
        Assert.Equal(food.Color, created.CategoryColor);
        Assert.EndsWith($"/api/expenses/{created.Id}", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);

        var fetched = (await user.Client.GetFromJsonAsync<ExpenseResponse>($"/api/expenses/{created.Id}"))!;
        Assert.Equal(250.5m, fetched.Amount);
        Assert.Equal(new DateTime(2026, 3, 5), fetched.Date);

        var list = await user.Client.ListExpensesAsync();
        Assert.Equal(created.Id, Assert.Single(list.Items).Id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1000001)]
    public async Task Create_RejectsAnAmountOutsideTheAllowedRange(int amount)
    {
        var user = await _factory.RegisterUserAsync();
        var food = await user.Client.CategoryNamedAsync("Food");

        var response = await user.Client.PostAsJsonAsync("/api/expenses",
            new ExpenseRequest { Amount = amount, CategoryId = food.Id, Date = DateTime.Today });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Amount", await response.MessageAsync());
        Assert.Empty((await user.Client.ListExpensesAsync()).Items);        // nothing was saved
    }

    [Fact]
    public async Task Create_RejectsARequestWithNoCategory()
    {
        var user = await _factory.RegisterUserAsync();

        var response = await user.Client.PostAsJsonAsync("/api/expenses",
            new ExpenseRequest { Amount = 10, CategoryId = 0, Date = DateTime.Today });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Please choose a category.", await response.MessageAsync());
    }

    [Fact]
    public async Task Create_RejectsACategoryThatDoesNotExist()
    {
        var user = await _factory.RegisterUserAsync();

        var response = await user.Client.PostAsJsonAsync("/api/expenses",
            new ExpenseRequest { Amount = 10, CategoryId = 999_999_999, Date = DateTime.Today });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Category does not exist.", await response.MessageAsync());
    }

    [Fact]
    public async Task Create_RejectsADateBeforeTheYear2000()
    {
        var user = await _factory.RegisterUserAsync();
        var food = await user.Client.CategoryNamedAsync("Food");

        var response = await user.Client.PostAsJsonAsync("/api/expenses",
            new ExpenseRequest { Amount = 10, CategoryId = food.Id, Date = new DateTime(1990, 1, 1) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Date", await response.MessageAsync());
    }

    [Fact]
    public async Task Update_ChangesTheExpense()
    {
        var user = await _factory.RegisterUserAsync();
        var food = await user.Client.CategoryNamedAsync("Food");
        var transport = await user.Client.CategoryNamedAsync("Transport");
        var expense = await user.Client.AddExpenseAsync(food.Id, 100, new DateTime(2026, 3, 5), "Old");

        var response = await user.Client.PutAsJsonAsync($"/api/expenses/{expense.Id}", new ExpenseRequest
        {
            Amount = 450,
            CategoryId = transport.Id,
            Date = new DateTime(2026, 3, 6),
            Description = "New"
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var saved = (await user.Client.GetFromJsonAsync<ExpenseResponse>($"/api/expenses/{expense.Id}"))!;
        Assert.Equal(450, saved.Amount);
        Assert.Equal("Transport", saved.Category);
        Assert.Equal("New", saved.Description);
        Assert.Equal(new DateTime(2026, 3, 6), saved.Date);
    }

    [Fact]
    public async Task Update_OfAnExpenseThatDoesNotExist_IsNotFound()
    {
        var user = await _factory.RegisterUserAsync();
        var food = await user.Client.CategoryNamedAsync("Food");

        var response = await user.Client.PutAsJsonAsync("/api/expenses/999999999",
            new ExpenseRequest { Amount = 1, CategoryId = food.Id, Date = DateTime.Today });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Expense not found.", await response.MessageAsync());
    }

    [Fact]
    public async Task Delete_RemovesTheExpense()
    {
        var user = await _factory.RegisterUserAsync();
        var food = await user.Client.CategoryNamedAsync("Food");
        var expense = await user.Client.AddExpenseAsync(food.Id, 100);

        var deleted = await user.Client.DeleteAsync($"/api/expenses/{expense.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var afterwards = await user.Client.GetAsync($"/api/expenses/{expense.Id}");
        Assert.Equal(HttpStatusCode.NotFound, afterwards.StatusCode);
        Assert.Equal("Expense not found.", await afterwards.MessageAsync());

        var again = await user.Client.DeleteAsync($"/api/expenses/{expense.Id}");
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
    }
}