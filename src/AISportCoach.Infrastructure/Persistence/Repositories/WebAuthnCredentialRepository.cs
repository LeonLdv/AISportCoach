using AISportCoach.Application.Interfaces;
using AISportCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AISportCoach.Infrastructure.Persistence.Repositories;

public class WebAuthnCredentialRepository(AppDbContext context) : IWebAuthnCredentialRepository
{
    public async Task<WebAuthnCredential?> GetByCredentialIdAsync(byte[] credentialId, CancellationToken cancellationToken)
        => await context.WebAuthnCredentials
            .FirstOrDefaultAsync(c => c.CredentialId == credentialId, cancellationToken);

    public async Task<List<WebAuthnCredential>> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken)
        => await context.WebAuthnCredentials
            .Where(c => c.UserId == userId && c.IsActive)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(WebAuthnCredential credential, CancellationToken cancellationToken)
    {
        await context.WebAuthnCredentials.AddAsync(credential, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(WebAuthnCredential credential, CancellationToken cancellationToken)
    {
        context.WebAuthnCredentials.Update(credential);
        await context.SaveChangesAsync(cancellationToken);
    }
}
