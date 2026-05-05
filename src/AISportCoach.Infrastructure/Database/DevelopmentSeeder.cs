using AISportCoach.Domain.Entities;
using AISportCoach.Domain.Enums;
using AISportCoach.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace AISportCoach.Infrastructure.Database;

public class DevelopmentSeeder(
    AppDbContext context,
    UserManager<ApplicationUser> userManager,
    ILogger<DevelopmentSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var testUserExists = await userManager.FindByEmailAsync("test@aisportcoach.local");
        if (testUserExists is not null)
        {
            logger.LogInformation("Development seed data already exists, skipping seeding");
            return;
        }

        logger.LogInformation("Seeding development test data");

        await SeedTestUsersAsync(cancellationToken);
        await SeedSampleDataAsync(cancellationToken);

        logger.LogInformation("Development data seeding completed");
    }

    private async Task SeedTestUsersAsync(CancellationToken cancellationToken)
    {
        var testUsers = new[]
        {
            new { Email = "test@aisportcoach.local", Password = "Test123!", DisplayName = "Test User", Role = "User", Tier = SubscriptionTier.Free },
            new { Email = "premium@aisportcoach.local", Password = "Premium123!", DisplayName = "Premium User", Role = "User", Tier = SubscriptionTier.Premium },
            new { Email = "admin@aisportcoach.local", Password = "Admin123!", DisplayName = "Admin User", Role = "Admin", Tier = SubscriptionTier.Admin }
        };

        foreach (var userData in testUsers)
        {
            var user = new ApplicationUser
            {
                Id = Guid.CreateVersion7(),
                UserName = userData.Email,
                Email = userData.Email,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(user, userData.Password);
            if (!createResult.Succeeded)
            {
                logger.LogWarning("Failed to create test user {Email}: {Errors}",
                    userData.Email, string.Join(", ", createResult.Errors.Select(e => e.Description)));
                continue;
            }

            var roleResult = await userManager.AddToRoleAsync(user, userData.Role);
            if (!roleResult.Succeeded)
            {
                logger.LogWarning("Failed to assign role {Role} to user {Email}: {Errors}",
                    userData.Role, userData.Email, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
            }

            var profile = UserProfile.Create(user.Id, userData.DisplayName);
            if (userData.Tier != SubscriptionTier.Free)
            {
                profile.UpdateSubscription(userData.Tier);
            }

            context.Set<UserProfile>().Add(profile);

            logger.LogInformation("Created test user {Email} with role {Role}", userData.Email, userData.Role);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedSampleDataAsync(CancellationToken cancellationToken)
    {
        var testUser = await userManager.FindByEmailAsync("test@aisportcoach.local");
        if (testUser is null)
        {
            logger.LogWarning("Test user not found, skipping sample data seeding");
            return;
        }

        var video1 = VideoUpload.Create(
            originalFileName: "forehand_practice.mp4",
            fileSizeBytes: 52428800,
            geminiFileUri: "gs://test-bucket/sample-forehand.mp4",
            userId: testUser.Id
        );
        video1.SetStatus(VideoStatus.Processed);

        var video2 = VideoUpload.Create(
            originalFileName: "backhand_slice.mp4",
            fileSizeBytes: 31457280,
            geminiFileUri: "gs://test-bucket/sample-backhand.mp4",
            userId: testUser.Id
        );
        video2.SetStatus(VideoStatus.Processing);

        context.Set<VideoUpload>().AddRange(video1, video2);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogDebug("Creating video upload for user {UserId}: {Title}", testUser.Id, "Forehand Practice Session");
        logger.LogDebug("Creating video upload for user {UserId}: {Title}", testUser.Id, "Backhand Slice Drill");

        var observations = new List<TechniqueObservation>
        {
            new()
            {
                Id = Guid.CreateVersion7(),
                CoachingReportId = Guid.Empty,
                Stroke = TennisStroke.Forehand,
                Description = "Strong wrist snap on contact",
                Severity = SeverityLevel.Info,
                FrameTimestamp = "00:01:23",
                BodyPart = "Wrist"
            },
            new()
            {
                Id = Guid.CreateVersion7(),
                CoachingReportId = Guid.Empty,
                Stroke = TennisStroke.Forehand,
                Description = "Inconsistent follow-through",
                Severity = SeverityLevel.Warning,
                FrameTimestamp = "00:02:15",
                BodyPart = "Shoulder"
            }
        };

        var recommendations = new List<ImprovementRecommendation>
        {
            new()
            {
                Id = Guid.CreateVersion7(),
                CoachingReportId = Guid.Empty,
                Title = "Focus on split-step timing",
                DetailedDescription = "Work on anticipating opponent's shot and executing split-step earlier",
                Priority = 1,
                TargetStroke = TennisStroke.Footwork,
                DrillSuggestions = ["Shadow swings with split-step", "Mirror drill with partner"]
            },
            new()
            {
                Id = Guid.CreateVersion7(),
                CoachingReportId = Guid.Empty,
                Title = "Increase shoulder rotation",
                DetailedDescription = "Generate more power through increased shoulder turn on backswing",
                Priority = 2,
                TargetStroke = TennisStroke.Forehand,
                DrillSuggestions = ["Wall rally focusing on rotation", "Resistance band exercises"]
            }
        };

        var ntrpEvidence = new List<NtrpEvidence>
        {
            new()
            {
                Id = Guid.CreateVersion7(),
                CoachingReportId = Guid.Empty,
                Observation = "Consistent rally ball depth",
                NtrpIndicator = "Can maintain rally with consistent depth",
                SupportedLevel = 3.5,
                Weight = "HIGH"
            },
            new()
            {
                Id = Guid.CreateVersion7(),
                CoachingReportId = Guid.Empty,
                Observation = "Needs improvement in movement efficiency",
                NtrpIndicator = "Footwork patterns show room for refinement",
                SupportedLevel = 3.0,
                Weight = "MEDIUM"
            }
        };

        var report = CoachingReport.Create(
            videoUploadId: video1.Id,
            overallScore: 75,
            executiveSummary: "Good topspin generation, needs work on footwork. Overall solid 3.5 level performance with room for improvement in consistency.",
            observations: observations,
            recommendations: recommendations,
            ntrpRating: 3.5,
            ntrpRatingMin: 3.0,
            ntrpRatingMax: 4.0,
            ntrpConfidence: "HIGH",
            ntrpRatingJustification: "Player demonstrates consistent groundstrokes with good technique but lacks advanced shot selection and court positioning typical of 4.0 level.",
            ntrpEvidence: ntrpEvidence
        );

        context.Set<CoachingReport>().Add(report);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogDebug("Creating coaching report for video {VideoId}", video1.Id);
        logger.LogInformation("Created {Count} sample videos for user {UserId}", 2, testUser.Id);
    }
}
