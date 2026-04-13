using AISportCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AISportCoach.Infrastructure.Persistence.Configurations;

public class ReportEmbeddingConfiguration : IEntityTypeConfiguration<ReportEmbedding>
{
    public void Configure(EntityTypeBuilder<ReportEmbedding> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.UserId).IsRequired();

        // The Embedding column is vector(768) — managed via raw SQL in the repository
        // because Npgsql EF Core 10 does not expose a UseVector() hook compatible with
        // the Aspire AddNpgsqlDbContext integration. The column is added in the migration.
        builder.Ignore(e => e.Embedding);

        builder.HasOne(e => e.CoachingReport)
            .WithMany()
            .HasForeignKey(e => e.CoachingReportId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
