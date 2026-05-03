using AISportCoach.Domain.Enums;

namespace AISportCoach.Application.Interfaces;

public interface ICurrentUserService
{
    Guid UserId { get; }
    string Email { get; }
    IEnumerable<string> Roles { get; }
    SubscriptionTier SubscriptionTier { get; }
    bool IsPremium { get; }
    bool IsAdmin { get; }
}
