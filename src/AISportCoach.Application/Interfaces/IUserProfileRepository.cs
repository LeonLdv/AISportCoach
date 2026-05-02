using AISportCoach.Domain.Entities;

namespace AISportCoach.Application.Interfaces;

public interface IUserProfileRepository
{
    Task<UserProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task AddAsync(UserProfile profile, CancellationToken cancellationToken);
    Task UpdateAsync(UserProfile profile, CancellationToken cancellationToken);
}
