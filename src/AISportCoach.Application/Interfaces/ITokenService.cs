using AISportCoach.Domain.Entities;
using AISportCoach.Domain.Enums;

namespace AISportCoach.Application.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(ApplicationUser user, IList<string> roles, SubscriptionTier subscriptionTier);
    string GenerateRefreshToken();
}
