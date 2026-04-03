using AISportCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AISportCoach.Infrastructure.Persistence.Configurations;

public class ImprovementRecommendationConfiguration : IEntityTypeConfiguration<ImprovementRecommendation>
{
    public void Configure(EntityTypeBuilder<ImprovementRecommendation> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.TargetStroke).HasConversion<string>();
        builder.Property(r => r.Title).HasMaxLength(500);
        builder.Property(r => r.DetailedDescription).HasMaxLength(2000);
        builder.Property(r => r.DrillSuggestions)
               .HasColumnType("jsonb");
    }
}
