using AISportCoach.Infrastructure.Database;
using AISportCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AISportCoach.API.Extensions;

public static class DatabaseExtensions
{
    public static async Task MigrateDatabaseAsync(
        this WebApplication app,
        CancellationToken cancellationToken = default)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        try
        {
            logger.LogInformation("Applying database migrations");
            await db.Database.MigrateAsync(cancellationToken);
            logger.LogInformation("Database migrations applied successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to apply database migrations");
            throw;
        }
    }

    public static async Task SeedDevelopmentDataAsync(
        this WebApplication app,
        CancellationToken cancellationToken = default)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DevelopmentSeeder>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DevelopmentSeeder>>();

        try
        {
            await seeder.SeedAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to seed development data");
        }
    }
}
