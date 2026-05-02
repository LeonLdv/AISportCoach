using AISportCoach.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AISportCoach.Infrastructure.Services;

public class EmailService(IConfiguration configuration, ILogger<EmailService> logger) : IEmailService
{
    public Task SendEmailConfirmationAsync(string email, string token, CancellationToken cancellationToken)
    {
        var urlTemplate = configuration["Email:ConfirmationUrlTemplate"] ?? "http://localhost:19006/confirm-email?token={0}";
        var url = string.Format(urlTemplate, token);

        logger.LogWarning(
            "EMAIL (console stub): To={Email}, Subject=Confirm your email, Link={Url}",
            email,
            url
        );

        // TODO: Replace with real email service (SendGrid, AWS SES, Azure Communication Services)
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string email, string token, CancellationToken cancellationToken)
    {
        var urlTemplate = configuration["Email:PasswordResetUrlTemplate"] ?? "http://localhost:19006/reset-password?token={0}";
        var url = string.Format(urlTemplate, token);

        logger.LogWarning(
            "EMAIL (console stub): To={Email}, Subject=Reset your password, Link={Url}",
            email,
            url
        );

        // TODO: Replace with real email service
        return Task.CompletedTask;
    }

    public Task SendWelcomeEmailAsync(string email, string displayName, CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "EMAIL (console stub): To={Email}, Subject=Welcome to AISportCoach, DisplayName={DisplayName}",
            email,
            displayName
        );

        // TODO: Replace with real email service
        return Task.CompletedTask;
    }
}
