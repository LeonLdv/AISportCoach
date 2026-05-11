using System.Text;
using AISportCoach.Domain.Entities;
using AISportCoach.Domain.Enums;
using AISportCoach.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace AISportCoach.API.Extensions;

public static class SecurityExtensions
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredLength = 8;

            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;

            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedEmail = false; // Set to true when email service is implemented
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        var jwtSecretKey = configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("JWT:SecretKey is not configured");
        var jwtIssuer = configuration["Jwt:Issuer"] ?? "AISportCoach.API";
        var jwtAudience = configuration["Jwt:Audience"] ?? "AISportCoach.WebApp";

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
                ClockSkew = TimeSpan.Zero
            };

            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    if (context.Exception is SecurityTokenExpiredException)
                    {
                        context.Response.Headers.Append("Token-Expired", "true");
                    }
                    return Task.CompletedTask;
                }
            };
        });

        return services;
    }

    public static IServiceCollection AddAuthorizationPolicies(
        this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy("Premium", policy => policy.RequireClaim(
                "subscription_tier",
                SubscriptionTier.Premium.ToString(),
                SubscriptionTier.Admin.ToString()))
            .AddPolicy("Admin", policy => policy.RequireRole("Admin"));

        return services;
    }
}
