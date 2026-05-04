using AISportCoach.Domain.Enums;

namespace AISportCoach.Domain.Entities;

public class UserProfile
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; } // FK to AspNetUsers

    // Business fields
    public string DisplayName { get; private set; } = string.Empty;
    public SubscriptionTier SubscriptionTier { get; private set; } = SubscriptionTier.Free;
    public string? ProfileImageUrl { get; private set; }
    public DateTime? LastLoginAt { get; private set; }

    // Audit fields
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; private set; }

    // Navigation properties
    public ApplicationUser User { get; private set; } = null!;

    // Factory method
    public static UserProfile Create(Guid userId, string displayName)
    {
        return new UserProfile
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            DisplayName = displayName,
            SubscriptionTier = SubscriptionTier.Free,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateSubscription(SubscriptionTier tier)
    {
        SubscriptionTier = tier;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateProfile(string displayName, string? profileImageUrl = null)
    {
        DisplayName = displayName;
        ProfileImageUrl = profileImageUrl;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateLastLogin()
    {
        LastLoginAt = DateTime.UtcNow;
    }
}
