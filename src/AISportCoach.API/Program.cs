#pragma warning disable SKEXP0070
using System.Text;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using AISportCoach.API;
using AISportCoach.API.Middleware;
using AISportCoach.Application.UseCases.UploadVideo;
using AISportCoach.Domain.Entities;
using AISportCoach.Domain.Enums;
using AISportCoach.Infrastructure;
using AISportCoach.Infrastructure.Persistence;
using AISportCoach.ServiceDefaults;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddUserSecrets<Program>(optional: true);

// Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Controllers + validation error factory
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
            new UnprocessableEntityObjectResult(
                new ValidationProblemDetails(context.ModelState)
                {
                    Status = StatusCodes.Status422UnprocessableEntity,
                    Instance = context.HttpContext.Request.Path
                })
            { ContentTypes = { "application/problem+json" } };
    });

// API versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// Swagger (JWT UI temporarily disabled - Swashbuckle 10.x compatibility issue with .NET 10)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.EnableAnnotations());
builder.Services.ConfigureOptions<ConfigureSwaggerOptions>();

// Note: To test auth endpoints manually, use curl or Postman with "Authorization: Bearer <token>" header

// MediatR — registers all handlers from Application assembly
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(UploadVideoCommand).Assembly));

// EF Core via Aspire integration (connection string injected by Aspire AppHost)
builder.AddNpgsqlDbContext<AppDbContext>("tenniscoach");

// HttpContextAccessor (required by CurrentUserService)
builder.Services.AddHttpContextAccessor();

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false; // Set to true when email service is implemented
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// JWT Authentication
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"]
    ?? throw new InvalidOperationException("JWT:SecretKey is not configured");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "AISportCoach.API";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "AISportCoach.WebApp";

builder.Services.AddAuthentication(options =>
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
        ClockSkew = TimeSpan.Zero // Strict expiry
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

// Authorization Policies
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Premium", policy => policy.RequireClaim(
        "subscription_tier",
        SubscriptionTier.Premium.ToString(),
        SubscriptionTier.Admin.ToString()))
    .AddPolicy("Admin", policy => policy.RequireRole("Admin"));

// HSTS configuration (production only)
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
    options.Preload = true;
});

// Infrastructure (repositories, SK kernel, video processing, background worker)
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Auto-migrate on startup (development convenience)
// if (app.Environment.IsDevelopment())
// {
//     using var scope = app.Services.CreateScope();
//     var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
//     await db.Database.MigrateAsync();
// }

app.UseMiddleware<ExceptionHandlingMiddleware>();

// HTTPS enforcement
if (!app.Environment.IsDevelopment())
{
    app.UseHsts(); // Enforce HTTPS for 1 year (default)
}
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
    foreach (var description in provider.ApiVersionDescriptions)
    {
        var label = description.IsDeprecated
            ? $"{description.GroupName} (deprecated)"
            : description.GroupName;
        c.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json",
            $"AI Tennis Coach API {label}");
    }
    c.RoutePrefix = "swagger";
});

app.MapControllers();
app.MapDefaultEndpoints(); // Aspire health check endpoints

app.Run();
