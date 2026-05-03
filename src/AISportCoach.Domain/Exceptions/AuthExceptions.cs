namespace AISportCoach.Domain.Exceptions;

public class InvalidCredentialsException(string? message = null)
    : DomainException(message ?? "Invalid email or password");

public class UserNotFoundException(string identifier)
    : DomainException($"User '{identifier}' not found");

public class UserAlreadyExistsException(string email)
    : DomainException($"User '{email}' already exists");

public class EmailNotConfirmedException(string email)
    : DomainException($"Email '{email}' is not confirmed. Check your inbox for confirmation link.");

public class InvalidTokenException(string tokenType)
    : DomainException($"Invalid or expired {tokenType} token");

public class SubscriptionRequiredException(string feature)
    : DomainException($"Premium subscription required to access {feature}");

public class WebAuthnVerificationException(string message)
    : DomainException(message);
