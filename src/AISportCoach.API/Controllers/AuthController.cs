using AISportCoach.API.DTOs;
using AISportCoach.Application.Interfaces;
using AISportCoach.Application.Options;
using AISportCoach.Domain.Entities;
using AISportCoach.Domain.Exceptions;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AISportCoach.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/auth")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Tags("Authentication")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IUserProfileRepository profileRepository,
    IRefreshTokenRepository refreshTokenRepository,
    ITokenService tokenService,
    IHttpContextAccessor httpContextAccessor,
    ILogger<AuthController> logger,
    IOptions<JwtOptions> jwtOptions) : ControllerBase
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;
    [HttpPost("register")]
    [EndpointSummary("Register new user")]
    [EndpointDescription("Creates a new user account and sends email confirmation.")]
    [ProducesResponseType<RegisterResponseDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request, CancellationToken ct)
    {
        logger.LogInformation("Registration attempt for Email={Email}", request.Email);

        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = request.Email,
            Email = request.Email
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return BadRequest(new ProblemDetails
            {
                Title = "Registration Failed",
                Detail = string.Join(", ", result.Errors.Select(e => e.Description))
            });

        // Assign default role
        await userManager.AddToRoleAsync(user, "User");

        // Create user profile
        var profile = UserProfile.Create(user.Id, request.DisplayName);
        await profileRepository.AddAsync(profile, ct);

        // Generate email confirmation token
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);

        logger.LogInformation("User registered successfully UserId={UserId}, Email={Email}", user.Id, user.Email);

        // TODO: Send confirmation email via IEmailService
        logger.LogWarning("Email confirmation token (not sent - implement IEmailService): {Token}", token);

        return CreatedAtAction(
            nameof(GetCurrentUser),
            new { userId = user.Id },
            new RegisterResponseDto(
                user.Id,
                user.Email!,
                request.DisplayName,
                "Registration successful. Please check your email to confirm your account."
            )
        );
    }

    [HttpPost("login")]
    [EndpointSummary("Login with email and password")]
    [EndpointDescription("Authenticates user and returns JWT access token and refresh token.")]
    [ProducesResponseType<AuthenticationResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken ct)
    {
        logger.LogInformation("Login attempt for Email={Email}", request.Email);

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            logger.LogWarning("Login failed - user not found Email={Email}", request.Email);
            throw new InvalidCredentialsException();
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            if (result.IsLockedOut)
            {
                logger.LogWarning("Login failed - account locked Email={Email}", request.Email);
                return Unauthorized(new ProblemDetails { Title = "Account Locked", Detail = "Account locked due to too many failed attempts" });
            }
            if (result.IsNotAllowed)
            {
                logger.LogWarning("Login failed - email not confirmed Email={Email}", request.Email);
                throw new EmailNotConfirmedException(request.Email);
            }

            logger.LogWarning("Login failed - invalid credentials Email={Email}", request.Email);
            throw new InvalidCredentialsException();
        }

        // Get user profile and roles
        var profile = await profileRepository.GetByUserIdAsync(user.Id, ct);
        var roles = await userManager.GetRolesAsync(user);

        // Update last login
        profile?.UpdateLastLogin();
        if (profile != null)
            await profileRepository.UpdateAsync(profile, ct);

        // Generate JWT tokens
        var accessToken = tokenService.GenerateAccessToken(user, roles.ToList(), profile?.SubscriptionTier ?? Domain.Enums.SubscriptionTier.Free);
        var refreshTokenValue = tokenService.GenerateRefreshToken();
        var ipAddress = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var refreshToken = RefreshToken.Create(
            user.Id,
            refreshTokenValue,
            DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpiryDays),
            ipAddress);
        await refreshTokenRepository.AddAsync(refreshToken, ct);

        logger.LogInformation("Login successful for UserId={UserId}", user.Id);

        return Ok(new AuthenticationResultDto(
            accessToken,
            refreshTokenValue,
            DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenExpiryMinutes),
            new UserDto(
                user.Id,
                user.Email!,
                profile?.DisplayName ?? user.Email!,
                (profile?.SubscriptionTier ?? Domain.Enums.SubscriptionTier.Free).ToString(),
                roles.ToList()
            )
        ));
    }

    [HttpPost("refresh")]
    [EndpointSummary("Refresh access token")]
    [EndpointDescription("Issues a new access token using a valid refresh token.")]
    [ProducesResponseType<AuthenticationResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshAccessToken([FromBody] RefreshTokenRequestDto request, CancellationToken ct)
    {
        logger.LogInformation("Token refresh attempt");

        var refreshToken = await refreshTokenRepository.GetByTokenAsync(request.RefreshToken, ct);
        if (refreshToken == null || !refreshToken.IsActive)
        {
            logger.LogWarning("Token refresh failed - invalid token");
            throw new InvalidTokenException("refresh");
        }

        var user = await userManager.FindByIdAsync(refreshToken.UserId.ToString());
        if (user == null)
        {
            logger.LogWarning("Token refresh failed - user not found UserId={UserId}", refreshToken.UserId);
            throw new UserNotFoundException(refreshToken.UserId.ToString());
        }

        var profile = await profileRepository.GetByUserIdAsync(user.Id, ct);
        var roles = await userManager.GetRolesAsync(user);

        // Token rotation: revoke old, issue new
        var ipAddress = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var newRefreshTokenValue = tokenService.GenerateRefreshToken();
        refreshToken.Revoke(ipAddress, newRefreshTokenValue);
        await refreshTokenRepository.UpdateAsync(refreshToken, ct);

        var newRefreshToken = RefreshToken.Create(user.Id, newRefreshTokenValue, DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpiryDays), ipAddress);
        await refreshTokenRepository.AddAsync(newRefreshToken, ct);

        var accessToken = tokenService.GenerateAccessToken(user, roles.ToList(), profile?.SubscriptionTier ?? Domain.Enums.SubscriptionTier.Free);

        logger.LogInformation("Token refreshed successfully for UserId={UserId}", user.Id);

        return Ok(new AuthenticationResultDto(
            accessToken,
            newRefreshTokenValue,
            DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenExpiryMinutes),
            new UserDto(
                user.Id,
                user.Email!,
                profile?.DisplayName ?? user.Email!,
                (profile?.SubscriptionTier ?? Domain.Enums.SubscriptionTier.Free).ToString(),
                roles.ToList()
            )
        ));
    }

    [HttpPost("logout")]
    // No [Authorize] - refresh token in body is sufficient for authorization
    // Allows logout even when access token has expired (user session cleanup)
    [EndpointSummary("Logout and revoke refresh token")]
    [EndpointDescription("Revokes the provided refresh token.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequestDto request, CancellationToken ct)
    {
        logger.LogInformation("Logout attempt");

        var refreshToken = await refreshTokenRepository.GetByTokenAsync(request.RefreshToken, ct);
        if (refreshToken != null && refreshToken.IsActive)
        {
            var ipAddress = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            refreshToken.Revoke(ipAddress);
            await refreshTokenRepository.UpdateAsync(refreshToken, ct);
            logger.LogInformation("User logged out successfully UserId={UserId}", refreshToken.UserId);
        }

        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    [EndpointSummary("Get current user profile")]
    [EndpointDescription("Returns the profile of the currently authenticated user.")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser(CancellationToken ct)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
            return NotFound();

        var profile = await profileRepository.GetByUserIdAsync(user.Id, ct);
        var roles = await userManager.GetRolesAsync(user);

        return Ok(new UserDto(
            user.Id,
            user.Email!,
            profile?.DisplayName ?? user.Email!,
            (profile?.SubscriptionTier ?? Domain.Enums.SubscriptionTier.Free).ToString(),
            roles.ToList()
        ));
    }
}
