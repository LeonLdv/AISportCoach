using AISportCoach.Domain.Entities;

namespace AISportCoach.Application.Interfaces;

public interface IWebAuthnCredentialRepository
{
    Task<WebAuthnCredential?> GetByCredentialIdAsync(byte[] credentialId, CancellationToken cancellationToken);
    Task<List<WebAuthnCredential>> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task AddAsync(WebAuthnCredential credential, CancellationToken cancellationToken);
    Task UpdateAsync(WebAuthnCredential credential, CancellationToken cancellationToken);
}
