using ExpenseTracker.Api.Common;
using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Services;
using ExpenseTracker.Tests.Helpers;

namespace ExpenseTracker.Tests.Unit;

public class BudgetServiceTests
{
	private const int Alice = 1;
	private const int Bob = 2;

	private static BudgetService ServiceFor(AppDbContext db, int userId) =>
		new(db, new FakeCurrentUser(userId));

	[Fact]
	public async Task GetStatus_AddsUpTheChosenMonthForEachBudgetedCategory()
	{
		using var db = TestDb.Create();
		var food = TestDb.AddCategory(db, Alice, "Food", budget: 1000);
		TestDb.AddCategory(db, Alice, "Transport", budget: 500);
		var rent = TestDb.AddCategory(db, Alice, "Rent");                       // no budget, so it is not listed

		TestDb.AddExpense(db, Alice, food.Id, 300, new DateTime(2026, 10, 5));
		TestDb.AddExpense(db, Alice, food.Id, 200, new DateTime(2026, 10, 31)); // the last day of the month counts
		TestDb.AddExpense(db, Alice, food.Id, 999, new DateTime(2026, 9, 30));  // the month before
		TestDb.AddExpense(db, Alice, food.Id, 888, new DateTime(2026, 11, 1));  // the month after
		TestDb.AddExpense(db, Alice, rent.Id, 12000, new DateTime(2026, 10, 1));

		var result = await ServiceFor(db, Alice).GetStatusAsync("2026-10");

		Assert.Equal(new[] { "Food", "Transport" }, result.Select(r => r.Name));

		var foodStatus = result.Single(r => r.Name == "Food");
		Assert.Equal(1000, foodStatus.Budget);
		Assert.Equal(500, foodStatus.Spent);

		Assert.Equal(0, result.Single(r => r.Name == "Transport").Spent);       // a budget with no spending
	}

	[Fact]
	public async Task GetStatus_IgnoresOtherUsers()
	{
		using var db = TestDb.Create();
		var bobsFood = TestDb.AddCategory(db, Bob, "Food", budget: 2000);
		TestDb.AddExpense(db, Bob, bobsFood.Id, 5000, new DateTime(2026, 10, 5));

		var result = await ServiceFor(db, Alice).GetStatusAsync("2026-10");

		Assert.Empty(result);
	}

	[Fact]
	public async Task GetStatus_UsesTheCurrentMonthWhenNoMonthIsGiven()
	{
		using var db = TestDb.Create();
		var food = TestDb.AddCategory(db, Alice, "Food", budget: 1000);
		TestDb.AddExpense(db, Alice, food.Id, 250, DateTime.Today);

		var result = await ServiceFor(db, Alice).GetStatusAsync(null);

		Assert.Equal(250, Assert.Single(result).Spent);
	}

	[Theory]
	[InlineData("2026-13")]     // there is no month 13
	[InlineData("October")]
	[InlineData("2026/10")]
	[InlineData("abc")]
	public async Task GetStatus_RejectsAMonthInTheWrongFormat(string month)
	{
		using var db = TestDb.Create();

		var ex = await Assert.ThrowsAsync<BadRequestException>(() => ServiceFor(db, Alice).GetStatusAsync(month));

		Assert.Contains("Month", ex.Message);
	}
}