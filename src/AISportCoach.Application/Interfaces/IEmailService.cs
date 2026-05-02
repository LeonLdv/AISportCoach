namespace AISportCoach.Application.Interfaces;

public interface IEmailService
{
    Task SendEmailConfirmationAsync(string email, string token, CancellationToken cancellationToken);
    Task SendPasswordResetAsync(string email, string token, CancellationToken cancellationToken);
    Task SendWelcomeEmailAsync(string email, string displayName, CancellationToken cancellationToken);
}
