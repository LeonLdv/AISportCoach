using AISportCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AISportCoach.Infrastructure.Persistence.Configurations;

public class VideoUploadConfiguration : IEntityTypeConfiguration<VideoUpload>
{
    public void Configure(EntityTypeBuilder<VideoUpload> builder)
    {
        builder.HasKey(v => v.Id);
        builder.Property(v => v.OriginalFileName).IsRequired().HasMaxLength(500);
        builder.Property(v => v.Status).HasConversion<string>();
        builder.Property(v => v.GeminiFileUri).HasMaxLength(2000);
    }
}
