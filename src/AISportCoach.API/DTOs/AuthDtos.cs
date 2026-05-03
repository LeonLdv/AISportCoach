using System.ComponentModel.DataAnnotations;

namespace AISportCoach.API.DTOs;

// Registration
public record RegisterRequestDto(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password,
    [Required, MaxLength(100)] string DisplayName
);

public record RegisterResponseDto(
    Guid UserId,
    string Email,
    string DisplayName,
    string Message
);

// Login
public record LoginRequestDto(
    [Required, EmailAddress] string Email,
    [Required] string Password
);

public record AuthenticationResultDto(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    UserDto User
);

public record UserDto(
    Guid Id,
    string Email,
    string DisplayName,
    string SubscriptionTier,
    List<string> Roles
);

// Token refresh
public record RefreshTokenRequestDto([Required] string RefreshToken);

// Logout
public record LogoutRequestDto([Required] string RefreshToken);

// Email confirmation
public record ConfirmEmailRequestDto(
    [Required] string UserId,
    [Required] string Token
);

// Password reset
public record ForgotPasswordRequestDto([Required, EmailAddress] string Email);

public record ResetPasswordRequestDto(
    [Required, EmailAddress] string Email,
    [Required] string Token,
    [Required, MinLength(8)] string NewPassword
);
