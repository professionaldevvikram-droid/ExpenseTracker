using System.Security.Claims;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ExpenseTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth)
    {
        _auth = auth;
    }

    // POST /api/auth/register
    [EnableRateLimiting("auth")]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        return await _auth.RegisterAsync(request);
    }

    // POST /api/auth/login
    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        return await _auth.LoginAsync(request);
    }

    // GET /api/auth/me   (needs a valid token)
    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserInfo>> Me()
    {
        if (!int.TryParse(User.FindFirstValue("sub"), out var userId))
            return Unauthorized();

        return await _auth.GetCurrentUserAsync(userId);
    }
}