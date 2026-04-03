using AISportCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AISportCoach.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<VideoUpload> VideoUploads => Set<VideoUpload>();
    public DbSet<CoachingReport> CoachingReports => Set<CoachingReport>();
    public DbSet<TechniqueObservation> TechniqueObservations => Set<TechniqueObservation>();
    public DbSet<ImprovementRecommendation> ImprovementRecommendations => Set<ImprovementRecommendation>();
    public DbSet<NtrpEvidence> NtrpEvidenceItems => Set<NtrpEvidence>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
