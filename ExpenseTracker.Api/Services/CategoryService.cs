using System.Linq.Expressions;
using ExpenseTracker.Api.Common;
using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Services;

public interface ICategoryService
{
    Task<List<CategoryResponse>> GetAllAsync();
    Task<CategoryResponse> GetByIdAsync(int id);
    Task<CategoryResponse> CreateAsync(CategoryRequest request);
    Task UpdateAsync(int id, CategoryRequest request);
    Task DeleteAsync(int id);
}

public class CategoryService : ICategoryService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _user;

    public CategoryService(AppDbContext db, ICurrentUser user)
    {
        _db = db;
        _user = user;
    }

    // How a Category becomes a CategoryResponse (including how many expenses use it)
    private Expression<Func<Category, CategoryResponse>> ToResponse => c =>
        new CategoryResponse(
            c.Id,
            c.Name,
            c.Color,
            c.MonthlyBudget,
            _db.Expenses.Count(e => e.CategoryId == c.Id));

    // Every query starts here, so a user can only ever reach their own categories
    private IQueryable<Category> MyCategories()
    {
        var userId = _user.Id;
        return _db.Categories.Where(c => c.UserId == userId);
    }

    public async Task<List<CategoryResponse>> GetAllAsync()
    {
        return await MyCategories()
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(ToResponse)
            .ToListAsync();
    }

    public async Task<CategoryResponse> GetByIdAsync(int id)
    {
        var category = await MyCategories()
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(ToResponse)
            .FirstOrDefaultAsync();

        return category ?? throw new NotFoundException("Category not found.");
    }

    public async Task<CategoryResponse> CreateAsync(CategoryRequest request)
    {
        var name = request.Name.Trim();

        if (await MyCategories().AnyAsync(c => c.Name == name))
            throw new ConflictException("A category with this name already exists.");

        var category = new Category
        {
            UserId = _user.Id,
            Name = name,
            Color = request.Color,
            MonthlyBudget = request.MonthlyBudget > 0 ? request.MonthlyBudget : null   // 0 means "no budget"
        };

        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        return await GetByIdAsync(category.Id);
    }

    public async Task UpdateAsync(int id, CategoryRequest request)
    {
        var category = await MyCategories().FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundException("Category not found.");

        var name = request.Name.Trim();

        if (await MyCategories().AnyAsync(c => c.Id != id && c.Name == name))
            throw new ConflictException("A category with this name already exists.");

        category.Name = name;
        category.Color = request.Color;
        category.MonthlyBudget = request.MonthlyBudget > 0 ? request.MonthlyBudget : null;

        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var category = await MyCategories().FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundException("Category not found.");

        if (await _db.Expenses.AnyAsync(e => e.CategoryId == id))
            throw new ConflictException("This category is used by existing expenses, so it can't be deleted.");

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
    }
}