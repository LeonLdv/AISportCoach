using AISportCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AISportCoach.Infrastructure.Persistence.Configurations;

public class NtrpEvidenceConfiguration : IEntityTypeConfiguration<NtrpEvidence>
{
    public void Configure(EntityTypeBuilder<NtrpEvidence> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Observation).HasMaxLength(1000);
        builder.Property(e => e.NtrpIndicator).HasMaxLength(500);
        builder.Property(e => e.Weight).HasMaxLength(20);
    }
}
