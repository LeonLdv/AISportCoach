using AISportCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AISportCoach.Infrastructure.Persistence.Configurations;

public class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.DisplayName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.SubscriptionTier)
            .HasConversion<string>();

        builder.Property(p => p.ProfileImageUrl)
            .HasMaxLength(2048);

        builder.HasIndex(p => p.UserId)
            .IsUnique();
    }
}
