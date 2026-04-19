using AISportCoach.Application.Interfaces;
using AISportCoach.Domain.Constants;

namespace AISportCoach.Infrastructure.Services;

public sealed class CurrentUserService : ICurrentUserService
{
    public Guid UserId =>
        // Placeholder — replace with real claim extraction when auth is added
        MockUser.Id;
}
