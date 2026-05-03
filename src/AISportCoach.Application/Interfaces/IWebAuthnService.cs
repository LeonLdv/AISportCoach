using AISportCoach.Domain.Entities;

namespace AISportCoach.Application.Interfaces;

public interface IWebAuthnService
{
    Task<object> BeginRegistrationAsync(Guid userId, CancellationToken cancellationToken);
    Task<WebAuthnCredential> CompleteRegistrationAsync(Guid userId, object response, CancellationToken cancellationToken);
    Task<object> BeginLoginAsync(string email, CancellationToken cancellationToken);
    Task<ApplicationUser> CompleteLoginAsync(object response, CancellationToken cancellationToken);
}
