using System.Security.Claims;
using AISportCoach.Application.Interfaces;
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
                throw new InvalidOperationException(
                    "UserId cannot be accessed without an active HTTP context. " +
                    "This typically means the request was made outside of an HTTP request scope.");

            var userId = GetClaimValue(ClaimTypes.NameIdentifier, Guid.Parse);
            if (userId == Guid.Empty)
                throw new InvalidOperationException(
                    "NameIdentifier claim is missing or invalid. " +
                    "Ensure the JWT token is valid and the middleware is configured correctly.");

            return userId;
        }
    }

    public string Email
    {
        get
        {
            if (httpContextAccessor.HttpContext?.User == null)
                throw new InvalidOperationException(
                    "Email cannot be accessed without an active HTTP context. " +
                    "This typically means the request was made outside of an HTTP request scope.");

            var email = GetClaimValue(ClaimTypes.Email, s => s);
            if (string.IsNullOrEmpty(email))
                throw new InvalidOperationException(
                    "Email claim is missing or empty. " +
                    "Ensure the JWT token is valid and the middleware is configured correctly.");

            return email;
        }
    }

    public IEnumerable<string> Roles
    {
        get
        {
            if (httpContextAccessor.HttpContext?.User == null)
                throw new InvalidOperationException(
                    "Roles cannot be accessed without an active HTTP context. " +
                    "This typically means the request was made outside of an HTTP request scope.");

            var roles = httpContextAccessor.HttpContext.User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
            if (roles.Count == 0)
                throw new InvalidOperationException(
                    "Role claims are missing. " +
                    "Ensure the JWT token is valid and includes role claims.");

            return roles;
        }
    }

    public SubscriptionTier SubscriptionTier
    {
        get
        {
            if (httpContextAccessor.HttpContext?.User == null)
                return SubscriptionTier.Free;

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
