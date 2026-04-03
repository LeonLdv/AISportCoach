using AISportCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AISportCoach.Infrastructure.Persistence.Configurations;

public class CoachingReportConfiguration : IEntityTypeConfiguration<CoachingReport>
{
    public void Configure(EntityTypeBuilder<CoachingReport> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.ExecutiveSummary).HasMaxLength(2000);
        builder.HasOne(r => r.VideoUpload)
               .WithMany()
               .HasForeignKey(r => r.VideoUploadId);
        builder.HasMany(r => r.Observations)
               .WithOne(o => o.CoachingReport)
               .HasForeignKey(o => o.CoachingReportId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(r => r.Recommendations)
               .WithOne(rec => rec.CoachingReport)
               .HasForeignKey(rec => rec.CoachingReportId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(r => r.NtrpEvidence)
               .WithOne(e => e.CoachingReport)
               .HasForeignKey(e => e.CoachingReportId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
