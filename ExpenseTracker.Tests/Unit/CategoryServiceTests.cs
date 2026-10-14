using ExpenseTracker.Api.Common;
using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Services;
using ExpenseTracker.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Tests.Unit;

public class CategoryServiceTests
{
    private const int Alice = 1;
    private const int Bob = 2;

    private static CategoryService ServiceFor(AppDbContext db, int userId) =>
        new(db, new FakeCurrentUser(userId));

    private static CategoryRequest Request(string name, decimal? budget = null) =>
        new() { Name = name, Color = "#4f46e5", MonthlyBudget = budget };

    [Fact]
    public async Task Create_SavesTheCategoryForTheCurrentUser_AndTrimsTheName()
    {
        using var db = TestDb.Create();

        var created = await ServiceFor(db, Alice).CreateAsync(Request("  Groceries  "));

        Assert.Equal("Groceries", created.Name);
        Assert.Equal(Alice, (await db.Categories.SingleAsync()).UserId);
    }

    [Fact]
    public async Task Create_TurnsABudgetOfZeroIntoNoBudget()
    {
        using var db = TestDb.Create();

        var created = await ServiceFor(db, Alice).CreateAsync(Request("Food", budget: 0));

        Assert.Null(created.MonthlyBudget);
    }

    [Fact]
    public async Task Create_RejectsADuplicateNameForTheSameUser()
    {
        using var db = TestDb.Create();
        TestDb.AddCategory(db, Alice, "Food");

        await Assert.ThrowsAsync<ConflictException>(() => ServiceFor(db, Alice).CreateAsync(Request("Food")));
    }

    [Fact]
    public async Task Create_AllowsTheSameNameForADifferentUser()
    {
        using var db = TestDb.Create();
        TestDb.AddCategory(db, Alice, "Food");

        await ServiceFor(db, Bob).CreateAsync(Request("Food"));

        Assert.Equal(2, await db.Categories.CountAsync());
    }

    [Fact]
    public async Task GetAll_ReturnsOnlyMyCategories_WithTheirExpenseCounts()
    {
        using var db = TestDb.Create();
        var alicesFood = TestDb.AddCategory(db, Alice, "Food");
        TestDb.AddCategory(db, Alice, "Bills");
        TestDb.AddCategory(db, Bob, "Secret");
        TestDb.AddExpense(db, Alice, alicesFood.Id, 10, DateTime.Today);
        TestDb.AddExpense(db, Alice, alicesFood.Id, 20, DateTime.Today);

        var result = await ServiceFor(db, Alice).GetAllAsync();

        Assert.Equal(new[] { "Bills", "Food" }, result.Select(c => c.Name));          // sorted by name
        Assert.Equal(2, result.Single(c => c.Name == "Food").ExpenseCount);
        Assert.Equal(0, result.Single(c => c.Name == "Bills").ExpenseCount);
    }

    [Fact]
    public async Task Update_ChangesTheNameColorAndBudget()
    {
        using var db = TestDb.Create();
        var category = TestDb.AddCategory(db, Alice, "Food");

        await ServiceFor(db, Alice).UpdateAsync(category.Id, new CategoryRequest
        {
            Name = "Eating out",
            Color = "#ff0000",
            MonthlyBudget = 2500
        });

        var saved = await db.Categories.SingleAsync();
        Assert.Equal("Eating out", saved.Name);
        Assert.Equal("#ff0000", saved.Color);
        Assert.Equal(2500, saved.MonthlyBudget);
    }

    [Fact]
    public async Task Update_AllowsKeepingTheSameName()
    {
        using var db = TestDb.Create();
        var category = TestDb.AddCategory(db, Alice, "Food");

        // Only the color changes. The name is "taken" only by this same category.
        await ServiceFor(db, Alice).UpdateAsync(category.Id, new CategoryRequest { Name = "Food", Color = "#00ff00" });

        Assert.Equal("#00ff00", (await db.Categories.SingleAsync()).Color);
    }

    [Fact]
    public async Task Update_RejectsANameThatAnotherCategoryAlreadyUses()
    {
        using var db = TestDb.Create();
        TestDb.AddCategory(db, Alice, "Food");
        var transport = TestDb.AddCategory(db, Alice, "Transport");

        await Assert.ThrowsAsync<ConflictException>(() =>
            ServiceFor(db, Alice).UpdateAsync(transport.Id, Request("Food")));
    }

    [Fact]
    public async Task Delete_RefusesWhileExpensesUseTheCategory()
    {
        using var db = TestDb.Create();
        var food = TestDb.AddCategory(db, Alice, "Food");
        TestDb.AddExpense(db, Alice, food.Id, 10, DateTime.Today);

        await Assert.ThrowsAsync<ConflictException>(() => ServiceFor(db, Alice).DeleteAsync(food.Id));

        Assert.Single(db.Categories);
    }

    [Fact]
    public async Task Delete_RemovesAnUnusedCategory()
    {
        using var db = TestDb.Create();
        var food = TestDb.AddCategory(db, Alice, "Food");

        await ServiceFor(db, Alice).DeleteAsync(food.Id);

        Assert.Empty(db.Categories);
    }

    [Fact]
    public async Task AnotherUsersCategory_BehavesLikeItDoesNotExist()
    {
        using var db = TestDb.Create();
        var alicesFood = TestDb.AddCategory(db, Alice, "Food");
        var bob = ServiceFor(db, Bob);

        await Assert.ThrowsAsync<NotFoundException>(() => bob.GetByIdAsync(alicesFood.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => bob.UpdateAsync(alicesFood.Id, Request("Hacked")));
        await Assert.ThrowsAsync<NotFoundException>(() => bob.DeleteAsync(alicesFood.Id));

        Assert.Equal("Food", (await db.Categories.SingleAsync()).Name);
    }
}