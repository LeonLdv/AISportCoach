using AISportCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AISportCoach.Infrastructure.Persistence.Configurations;

public class WebAuthnCredentialConfiguration : IEntityTypeConfiguration<WebAuthnCredential>
{
    public void Configure(EntityTypeBuilder<WebAuthnCredential> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.CredentialId)
            .IsRequired()
            .HasMaxLength(1024); // WebAuthn credential IDs can be large

        builder.Property(c => c.PublicKey)
            .IsRequired();

        builder.HasIndex(c => new { c.UserId, c.CredentialId });

        builder.Property(c => c.DeviceInfo)
            .IsRequired()
            .HasMaxLength(1000);
    }
}
