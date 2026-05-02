using AISportCoach.Domain.Entities;

namespace AISportCoach.Application.Interfaces;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken);
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken);
    Task UpdateAsync(RefreshToken token, CancellationToken cancellationToken);
    Task RevokeAllForUserAsync(Guid userId, string revokedByIp, CancellationToken cancellationToken);
}
