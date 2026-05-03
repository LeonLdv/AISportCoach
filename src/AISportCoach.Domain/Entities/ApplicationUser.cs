using Microsoft.AspNetCore.Identity;

namespace AISportCoach.Domain.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    // Navigation to custom business profile (1:1)
    public UserProfile? Profile { get; set; }

    // Navigation to auth-related entities
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<WebAuthnCredential> WebAuthnCredentials { get; set; } = new List<WebAuthnCredential>();
}
