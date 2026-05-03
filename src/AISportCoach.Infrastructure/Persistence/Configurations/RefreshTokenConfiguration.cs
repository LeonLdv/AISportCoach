using AISportCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AISportCoach.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Token)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(t => t.Token)
            .IsUnique();

        builder.HasIndex(t => new { t.UserId, t.ExpiresAt });

        builder.Property(t => t.CreatedByIp)
            .IsRequired()
            .HasMaxLength(45); // IPv6 max length

        builder.Property(t => t.RevokedByIp)
            .HasMaxLength(45);

        builder.Property(t => t.ReplacedByToken)
            .HasMaxLength(256);
    }
}
