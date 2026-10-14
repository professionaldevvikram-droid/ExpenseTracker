using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Tests.Helpers;

// Pretends to be the logged-in user, so tests don't need a real token
public class FakeCurrentUser : ICurrentUser
{
    public FakeCurrentUser(int id) { Id = id; }
    public int Id { get; set; }
}

public static class TestDb
{
    // A brand-new, empty, in-memory database for each test, so tests can't affect each other
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    public static Category AddCategory(AppDbContext db, int userId, string name, decimal? budget = null)
    {
        var category = new Category { UserId = userId, Name = name, Color = "#112233", MonthlyBudget = budget };
        db.Categories.Add(category);
        db.SaveChanges();
        return category;
    }

    public static Expense AddExpense(
        AppDbContext db, int userId, int categoryId, decimal amount, DateTime date, string? description = null)
    {
        var expense = new Expense
        {
            UserId = userId,
            CategoryId = categoryId,
            Amount = amount,
            Date = date,
            Description = description
        };
        db.Expenses.Add(expense);
        db.SaveChanges();
        return expense;
    }
}