#pragma warning disable SKEXP0070
using System.Text;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using AISportCoach.API;
using AISportCoach.API.Extensions;
using AISportCoach.API.Middleware;
using AISportCoach.Application.UseCases.UploadVideo;
using AISportCoach.Domain.Entities;
using AISportCoach.Domain.Enums;
using AISportCoach.Infrastructure;
using AISportCoach.Infrastructure.Persistence;
using AISportCoach.ServiceDefaults;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Debug helper: Uncomment to pause startup and attach debugger in Rider
// if (builder.Environment.IsDevelopment() && Environment.GetEnvironmentVariable("WAIT_FOR_DEBUGGER") == "1")
// {
//     Console.WriteLine($"Waiting for debugger to attach. Process ID: {Environment.ProcessId}");
//     Console.WriteLine("Press any key to continue after attaching debugger...");
//     Console.ReadKey();
// }

builder.Configuration.AddUserSecrets<Program>(optional: true);

// Configure Kestrel for large file uploads (driven by VideoStorage:MaxFileSizeMB)
var maxUploadBytes = builder.Configuration.GetValue<long>("VideoStorage:MaxFileSizeMB", 500) * 1024 * 1024;
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = maxUploadBytes;
});

// Aspire service defaults (telemetry, health checks, service discovery)
// ServiceDefaults now includes conditional 10-minute timeouts for VideoFileService
builder.AddServiceDefaults();

// Request timeout middleware (10 minutes for video uploads)
builder.Services.AddRequestTimeouts(options =>
{
    options.AddPolicy("VideoUpload", TimeSpan.FromMinutes(10));
});

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

// Configure form options for large file uploads (driven by VideoStorage:MaxFileSizeMB)
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = maxUploadBytes;
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

// Swagger with JWT bearer authentication support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.EnableAnnotations();
    c.AddJwtAuthentication();
});
builder.Services.ConfigureOptions<ConfigureSwaggerOptions>();

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

// Database initialization (Development only)
if (app.Environment.IsDevelopment())
{
    await app.MigrateDatabaseAsync();
    await app.SeedDevelopmentDataAsync();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

// HTTPS enforcement (disabled in Development to support functional tests)
if (!app.Environment.IsDevelopment())
{
    app.UseHsts(); // Enforce HTTPS for 1 year (default)
    app.UseHttpsRedirection();
}

app.UseRequestTimeouts();

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
