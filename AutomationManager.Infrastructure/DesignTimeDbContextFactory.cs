using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace AutomationManager.Infrastructure;

/// <summary>
/// Factory used by EF Core tools (dotnet ef) at design time to create the DbContext
/// for migration generation. Uses the API project's appsettings for the connection string,
/// falling back to a default local PostgreSQL connection.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AutomationDbContext>
{
    public AutomationDbContext CreateDbContext(string[] args)
    {
        // Try to load configuration from the API project
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "AutomationManager.API");
        
        IConfiguration? configuration = null;
        if (Directory.Exists(basePath))
        {
            configuration = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .Build();
        }

        var connectionString = configuration?.GetConnectionString("DefaultConnection");

        // If the connection string is missing or InMemory, use a default PostgreSQL connection for migrations
        if (string.IsNullOrEmpty(connectionString) || connectionString.Contains("InMemory"))
        {
            connectionString = "Host=localhost;Database=AutomationManager;Username=postgres;Password=password";
        }

        var optionsBuilder = new DbContextOptionsBuilder<AutomationDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new AutomationDbContext(optionsBuilder.Options);
    }
}
