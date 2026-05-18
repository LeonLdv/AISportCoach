#pragma warning disable SKEXP0070
using Asp.Versioning;
using AISportCoach.API;
using AISportCoach.API.Extensions;
using AISportCoach.API.Middleware;
using AISportCoach.Application.UseCases.UploadVideo;
using AISportCoach.Infrastructure;
using AISportCoach.Infrastructure.Persistence;
using AISportCoach.ServiceDefaults;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);


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

builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorizationPolicies();

// HSTS configuration (production only)
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
    options.Preload = true;
});

builder.Services.AddCorsPolicy(builder.Configuration);
builder.Services.AddAuthRateLimiter(builder.Configuration);

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

app.UseCors();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
    app.UseSwaggerWithVersioning();

app.MapControllers();
app.MapDefaultEndpoints(); // Aspire health check endpoints

app.Run();
