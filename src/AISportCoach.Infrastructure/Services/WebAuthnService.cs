using AISportCoach.Application.Interfaces;
using AISportCoach.Domain.Entities;

namespace AISportCoach.Infrastructure.Services;

public class WebAuthnService : IWebAuthnService
{
    public Task<object> BeginRegistrationAsync(Guid userId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("WebAuthn is not yet implemented.");
    }

    public Task<WebAuthnCredential> CompleteRegistrationAsync(Guid userId, object response, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("WebAuthn is not yet implemented.");
    }

    public Task<object> BeginLoginAsync(string email, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("WebAuthn is not yet implemented.");
    }

    public Task<ApplicationUser> CompleteLoginAsync(object response, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("WebAuthn is not yet implemented.");
    }
}
