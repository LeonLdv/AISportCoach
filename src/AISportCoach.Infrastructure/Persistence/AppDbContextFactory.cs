using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AISportCoach.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF Core migrations.
/// Creates DbContext without HttpContextAccessor, which triggers system user fallback in SaveChangesAsync.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        // Use a dummy connection string - migrations don't need a real connection
        // The actual connection string comes from appsettings.json at runtime
        optionsBuilder.UseNpgsql("Host=localhost;Database=tenniscoach;Username=postgres;Password=postgres");

        // Factory creates DbContext without HttpContextAccessor (migrations don't need it)
        return new AppDbContext(optionsBuilder.Options);
    }
}
