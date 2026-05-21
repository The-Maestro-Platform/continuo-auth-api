using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Continuo.Observability;
using Continuo.Persistence;

namespace AuthApi.Data;

public class AuthDbContextFactory : IDesignTimeDbContextFactory<AuthDbContext> {
    public AuthDbContext CreateDbContext(string[] args) {
        // Ensure .env is loaded for design-time tooling (migrations)
        ContinuoEnvironment.EnsureLoaded();
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<AuthDbContext>();
        var connectionString = PersistenceExtensions.ResolveConnectionString(configuration, "auth-api");
        var schema = SchemaTools.ResolveSchema(configuration, "auth-api");

        if (!string.IsNullOrWhiteSpace(schema)) {
            Environment.SetEnvironmentVariable("DB_SCHEMA", schema);
        }

        optionsBuilder.UseSqlServer(connectionString, sql => {
            sql.MigrationsAssembly(typeof(Program).Assembly.GetName().Name);
            SchemaTools.ConfigureHistory(sql, schema);
            // SQL Server 2014 compat to avoid OPENJSON in generated SQL (prod DB level < 130).
            sql.UseCompatibilityLevel(120);
            sql.EnableRetryOnFailure();
        });

        return new AuthDbContext(optionsBuilder.Options);
    }
}
