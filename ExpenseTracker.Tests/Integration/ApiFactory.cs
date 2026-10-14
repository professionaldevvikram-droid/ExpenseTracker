using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ExpenseTracker.Tests.Integration;

// Starts your real API inside the test process, wired to a throwaway database.
// (Any public class from the API project tells the factory which application to start.)
public class ApiFactory : WebApplicationFactory<ExpenseService>
{
    public const string JwtKey = "integration-test-signing-key-that-is-long-enough-1234567890";

    private readonly string _connectionString;
    private bool _cleanedUp;

    public ApiFactory()
    {
        var database = $"ExpenseTrackerTests_{Guid.NewGuid():N}";
        _connectionString =
            $"Server=(localdb)\\MSSQLLocalDB;Database={database};Trusted_Connection=True;TrustServerCertificate=True;";

        // Environment variables are the strongest setting source, so the API starts with these values.
        // They are set before the API starts, because Program.cs reads the JWT key straight away.
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _connectionString);
        Environment.SetEnvironmentVariable("Jwt__Key", JwtKey);
        Environment.SetEnvironmentVariable("Serilog__WriteTo__1__Args__path",
            Path.Combine(Path.GetTempPath(), "expense-tracker-tests-.log"));      // keeps test logs out of your project
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Lets each test choose the IP address a request appears to come from
        builder.ConfigureTestServices(services =>
            services.AddTransient<IStartupFilter, TestIpStartupFilter>());
    }

    // After the API is built, create the empty test database and apply your real migrations
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();

        return host;
    }

    // Delete the throwaway database when the tests are done
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing || _cleanedUp) return;
        _cleanedUp = true;

        try
        {
            SqlConnection.ClearAllPools();     // let go of connections still open to the test database

            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(_connectionString).Options;
            using var db = new AppDbContext(options);
            db.Database.EnsureDeleted();
        }
        catch
        {
            // A leftover test database is harmless (the cleanup script at the end removes it)
        }
    }
}