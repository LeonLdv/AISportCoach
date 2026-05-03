using System.Security.Claims;
using AISportCoach.Application.Interfaces;
using AISportCoach.Domain.Constants;
using AISportCoach.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace AISportCoach.Infrastructure.Services;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{

    public Guid UserId
    {
        get
        {
            if (httpContextAccessor.HttpContext?.User == null)
                return SystemUser.Id;

            var userId = GetClaimValue(ClaimTypes.NameIdentifier, Guid.Parse);
            return userId != default ? userId : SystemUser.Id;
        }
    }

    public string Email
    {
        get
        {
            if (httpContextAccessor.HttpContext?.User == null)
                return SystemUser.Email;

            return GetClaimValue(ClaimTypes.Email, s => s) ?? SystemUser.Email;
        }
    }

    public IEnumerable<string> Roles
    {
        get
        {
            if (httpContextAccessor.HttpContext?.User == null)
                return new[] { "Admin" };

            return httpContextAccessor.HttpContext.User.FindAll(ClaimTypes.Role).Select(c => c.Value);
        }
    }

    public SubscriptionTier SubscriptionTier
    {
        get
        {
            if (httpContextAccessor.HttpContext?.User == null)
                return SubscriptionTier.Admin;

            var tier = GetClaimValue("subscription_tier", s => Enum.Parse<SubscriptionTier>(s));
            return tier != default ? tier : SubscriptionTier.Free;
        }
    }

    public bool IsPremium =>
        SubscriptionTier is SubscriptionTier.Premium or SubscriptionTier.Admin;

    public bool IsAdmin =>
        Roles.Contains("Admin");

    private T? GetClaimValue<T>(string claimType, Func<string, T> converter)
    {
        var claim = httpContextAccessor.HttpContext?.User?.FindFirst(claimType);
        return claim != null ? converter(claim.Value) : default;
    }
}
