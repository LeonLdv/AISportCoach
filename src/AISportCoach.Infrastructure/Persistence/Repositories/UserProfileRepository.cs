using AISportCoach.Application.Interfaces;
using AISportCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AISportCoach.Infrastructure.Persistence.Repositories;

public class UserProfileRepository(AppDbContext context) : IUserProfileRepository
{
    public async Task<UserProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
        => await context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

    public async Task AddAsync(UserProfile profile, CancellationToken cancellationToken)
    {
        await context.UserProfiles.AddAsync(profile, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(UserProfile profile, CancellationToken cancellationToken)
    {
        context.UserProfiles.Update(profile);
        await context.SaveChangesAsync(cancellationToken);
    }
}
