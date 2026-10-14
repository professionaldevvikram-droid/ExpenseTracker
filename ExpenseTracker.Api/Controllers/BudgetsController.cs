using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace ExpenseTracker.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class BudgetsController : ControllerBase
{
    private readonly IBudgetService _budgets;

    public BudgetsController(IBudgetService budgets)
    {
        _budgets = budgets;
    }

    // GET /api/budgets/status            -> current month
    // GET /api/budgets/status?month=2026-09
    [HttpGet("status")]
    public async Task<ActionResult<List<BudgetStatus>>> GetStatus([FromQuery] string? month)
    {
        return await _budgets.GetStatusAsync(month);
    }
}