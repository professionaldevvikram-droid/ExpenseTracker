using ExpenseTracker.Api.Common;

namespace ExpenseTracker.Api.Middleware;

// Turns exceptions into JSON responses like { "message": "..." }
public class ExceptionHandlingMiddleware
{
	private readonly RequestDelegate _next;
	private readonly ILogger<ExceptionHandlingMiddleware> _logger;

	public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
	{
		_next = next;
		_logger = logger;
	}

	public async Task InvokeAsync(HttpContext context)
	{
		try
		{
			await _next(context);
		}
		catch (AppException ex)
		{
			// Expected problems: not found, conflict, bad input
			await WriteAsync(context, ex.StatusCode, ex.Message);
		}
		catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
		{
			// The browser closed the request, so there is nobody to answer
		}
		catch (Exception ex)
		{
			// Unexpected problems: log the details, but don't show them to the user
			_logger.LogError(ex, "Unhandled exception on {Method} {Path}",
				context.Request.Method, context.Request.Path);
			await WriteAsync(context, StatusCodes.Status500InternalServerError,
				"Something went wrong on the server.");
		}
	}

	private static async Task WriteAsync(HttpContext context, int statusCode, string message)
	{
		if (context.Response.HasStarted) return;

		context.Response.StatusCode = statusCode;
		await context.Response.WriteAsJsonAsync(new { message });
	}
}