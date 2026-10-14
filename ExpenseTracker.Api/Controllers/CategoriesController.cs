using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace ExpenseTracker.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categories;

    public CategoriesController(ICategoryService categories)
    {
        _categories = categories;
    }

    // GET /api/categories
    [HttpGet]
    public async Task<ActionResult<List<CategoryResponse>>> GetAll()
    {
        return await _categories.GetAllAsync();
    }

    // GET /api/categories/3
    [HttpGet("{id:int}")]
    public async Task<ActionResult<CategoryResponse>> GetById(int id)
    {
        return await _categories.GetByIdAsync(id);
    }

    // POST /api/categories
    [HttpPost]
    public async Task<ActionResult<CategoryResponse>> Create(CategoryRequest request)
    {
        var created = await _categories.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    // PUT /api/categories/3
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CategoryRequest request)
    {
        await _categories.UpdateAsync(id, request);
        return NoContent();
    }

    // DELETE /api/categories/3
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _categories.DeleteAsync(id);
        return NoContent();
    }
}