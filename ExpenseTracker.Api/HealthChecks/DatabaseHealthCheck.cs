using ExpenseTracker.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ExpenseTracker.Api.HealthChecks;

// Answers one question: can the app reach its database?
public class DatabaseHealthCheck : IHealthCheck
{
	private readonly AppDbContext _db;

	public DatabaseHealthCheck(AppDbContext db)
	{
		_db = db;
	}

	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context, CancellationToken cancellationToken = default)
	{
		try
		{
			return await _db.Database.CanConnectAsync(cancellationToken)
				? HealthCheckResult.Healthy("The database is reachable.")
				: HealthCheckResult.Unhealthy("Cannot connect to the database.");
		}
		catch (Exception ex)
		{
			return HealthCheckResult.Unhealthy("The database check failed.", ex);
		}
	}
}