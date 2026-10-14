using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace ExpenseTracker.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenses;
    private readonly IExportService _export;

    public ExpensesController(IExpenseService expenses, IExportService export)
    {
        _expenses = expenses;
        _export = export;
    }

    // GET /api/expenses?page=2&pageSize=10&categoryId=1&from=2026-10-01&sort=amount_desc
    [HttpGet]
    public async Task<ActionResult<PagedResult<ExpenseResponse>>> GetAll([FromQuery] ExpenseQuery filter)
    {
        return await _expenses.GetPagedAsync(filter);
    }

    // GET /api/expenses/summary
    [HttpGet("summary")]
    public async Task<ActionResult<ExpenseSummary>> GetSummary()
    {
        return await _expenses.GetSummaryAsync();
    }

    // GET /api/expenses/export?from=2026-10-01&to=2026-10-31
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var file = await _export.ExportExpensesAsync(from, to);
        return File(file.Content, file.ContentType, file.FileName);
    }

    // GET /api/expenses/5
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ExpenseResponse>> GetById(int id)
    {
        return await _expenses.GetByIdAsync(id);
    }

    // POST /api/expenses
    [HttpPost]
    public async Task<ActionResult<ExpenseResponse>> Create(ExpenseRequest request)
    {
        var created = await _expenses.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    // PUT /api/expenses/5
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ExpenseRequest request)
    {
        await _expenses.UpdateAsync(id, request);
        return NoContent();
    }

    // DELETE /api/expenses/5
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _expenses.DeleteAsync(id);
        return NoContent();
    }
}