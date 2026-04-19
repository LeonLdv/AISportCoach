using AISportCoach.Application.Interfaces;
using AISportCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AISportCoach.Infrastructure.Persistence;

public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ICurrentUserService currentUserService) : DbContext(options)
{
    public DbSet<VideoUpload> VideoUploads => Set<VideoUpload>();
    public DbSet<CoachingReport> CoachingReports => Set<CoachingReport>();
    public DbSet<TechniqueObservation> TechniqueObservations => Set<TechniqueObservation>();
    public DbSet<ImprovementRecommendation> ImprovementRecommendations => Set<ImprovementRecommendation>();
    public DbSet<NtrpEvidence> NtrpEvidenceItems => Set<NtrpEvidence>();
    public DbSet<ReportEmbedding> ReportEmbeddings => Set<ReportEmbedding>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        var userId = currentUserService.UserId;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State is EntityState.Added)
                entry.Entity.SetCreated(userId, utcNow);
            else if (entry.State is EntityState.Modified)
                entry.Entity.SetModified(userId, utcNow);
        }

        return await base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
