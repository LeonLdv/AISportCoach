using AISportCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AISportCoach.Infrastructure.Persistence.Configurations;

public class TechniqueObservationConfiguration : IEntityTypeConfiguration<TechniqueObservation>
{
    public void Configure(EntityTypeBuilder<TechniqueObservation> builder)
    {
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Stroke).HasConversion<string>();
        builder.Property(o => o.Severity).HasConversion<string>();
        builder.Property(o => o.Description).HasMaxLength(1000);
        builder.Property(o => o.BodyPart).HasMaxLength(100);
        builder.Property(o => o.FrameTimestamp).HasMaxLength(50);
    }
}
