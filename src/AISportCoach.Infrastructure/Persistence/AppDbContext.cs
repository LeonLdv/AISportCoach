using AISportCoach.Application.Interfaces;
using AISportCoach.Domain.Constants;
using AISportCoach.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AISportCoach.Infrastructure.Persistence;

public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    IHttpContextAccessor? httpContextAccessor = null)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    private readonly IHttpContextAccessor? _httpContextAccessor = httpContextAccessor;
    // Identity tables (automatically created by IdentityDbContext)
    // - AspNetUsers
    // - AspNetRoles
    // - AspNetUserRoles
    // - AspNetUserClaims
    // - AspNetUserLogins
    // - AspNetRoleClaims
    // - AspNetUserTokens

    // Custom tables
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<WebAuthnCredential> WebAuthnCredentials => Set<WebAuthnCredential>();

    public DbSet<VideoUpload> VideoUploads => Set<VideoUpload>();
    public DbSet<CoachingReport> CoachingReports => Set<CoachingReport>();
    public DbSet<TechniqueObservation> TechniqueObservations => Set<TechniqueObservation>();
    public DbSet<ImprovementRecommendation> ImprovementRecommendations => Set<ImprovementRecommendation>();
    public DbSet<NtrpEvidence> NtrpEvidenceItems => Set<NtrpEvidence>();
    public DbSet<ReportEmbedding> ReportEmbeddings => Set<ReportEmbedding>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;

        // Resolve ICurrentUserService from HttpContext (DbContext pooling compatible)
        // Falls back to system user if no HttpContext available (e.g., migrations, background jobs)
        var currentUserService = _httpContextAccessor?.HttpContext?.RequestServices.GetService<ICurrentUserService>();
        var userId = currentUserService?.UserId ?? SystemUser.Id;

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
