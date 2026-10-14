using System.Text;
using System.Threading.RateLimiting;
using ExpenseTracker.Api.Common;
using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.HealthChecks;
using ExpenseTracker.Api.Middleware;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;
using ExpenseTracker.Api.OpenApi;
using Scalar.AspNetCore;

// A simple logger that still works if the app fails while starting up
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Replace the default logging with Serilog, configured from appsettings.json
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddControllers()
        .ConfigureApiBehaviorOptions(options =>
        {
            // Validation errors use the same { "message": "..." } shape as every other error
            options.InvalidModelStateResponseFactory = context =>
            {
                var message = context.ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m)) ?? "The request is not valid.";
                return new BadRequestObjectResult(new { message });
            };
        });

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

    /* ---------- JWT settings ---------- */

    var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
    if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
    {
        throw new InvalidOperationException(
            "Jwt:Key is missing or too short. Add a random key of at least 32 characters to user secrets.");
    }
    builder.Services.AddSingleton(jwt);

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;      // keep claim names such as "sub" and "email" exactly as written
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwt.Issuer,
                ValidateAudience = true,
                ValidAudience = jwt.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            };
        });

    builder.Services.AddAuthorization();

    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer((document, context, cancellationToken) =>
        {
            document.Info = new()
            {
                Title = "Expense Tracker API",
                Version = "v1",
                Description = "Track expenses, categories and monthly budgets."
            };
            return Task.CompletedTask;
        });

        options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    });
    /* ---------- Rate limiting ---------- */

    // Login and register: at most 10 attempts per minute from one IP address.
    // This slows down anyone trying thousands of passwords.
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.AddPolicy("auth", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

        options.OnRejected = async (context, cancellationToken) =>
        {
            var response = context.HttpContext.Response;
            response.StatusCode = StatusCodes.Status429TooManyRequests;

            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                response.Headers["Retry-After"] = ((int)retryAfter.TotalSeconds).ToString();

            await response.WriteAsJsonAsync(
                new { message = "Too many attempts. Please wait a minute and try again." },
                cancellationToken);
        };
    });

    /* ---------- Health check ---------- */

    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>("database");

    /* ---------- Services ---------- */

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, CurrentUser>();

    builder.Services.AddSingleton<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
    builder.Services.AddSingleton<ITokenService, TokenService>();
    builder.Services.AddScoped<IAuthService, AuthService>();

    builder.Services.AddScoped<IExpenseService, ExpenseService>();
    builder.Services.AddScoped<ICategoryService, CategoryService>();
    builder.Services.AddScoped<IBudgetService, BudgetService>();
    builder.Services.AddScoped<IExportService, ExcelExportService>();

    var app = builder.Build();

    // One log line per API request: "HTTP GET /api/expenses responded 200 in 12.4 ms"
    app.UseSerilogRequestLogging(options =>
    {
        options.GetLevel = (httpContext, _, ex) =>
        {
            if (ex != null || httpContext.Response.StatusCode >= 500) return LogEventLevel.Error;
            if (httpContext.Request.Path.StartsWithSegments("/api")) return LogEventLevel.Information;
            return LogEventLevel.Debug;      // pages, scripts, styles and /health stay out of the log
        };
    });

    app.UseMiddleware<ExceptionHandlingMiddleware>();   // catches errors from everything after it
    app.UseMiddleware<SecurityHeadersMiddleware>();

    app.UseDefaultFiles();   // looks for wwwroot/index.html
    app.UseStaticFiles();    // serves files from wwwroot (public, so the login page can load)

    app.UseRateLimiter();

    app.UseAuthentication(); // who is calling?
    app.UseAuthorization();  // are they allowed in?

    app.MapControllers();
    app.MapHealthChecks("/health");
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();               // the machine-readable description: /openapi/v1.json
        app.MapScalarApiReference();    // the readable, interactive page: /scalar
    }

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    // HostAbortedException is thrown on purpose by "dotnet ef", so it is not a real failure
    Log.Fatal(ex, "The application stopped because of an error during startup");
}
finally
{
    Log.CloseAndFlush();
}