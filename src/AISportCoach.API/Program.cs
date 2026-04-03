#pragma warning disable SKEXP0070
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using AISportCoach.API;
using AISportCoach.API.Middleware;
using AISportCoach.Application.UseCases.UploadVideo;
using AISportCoach.Infrastructure;
using AISportCoach.Infrastructure.Persistence;
using AISportCoach.ServiceDefaults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddUserSecrets<Program>(optional: true);

// Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Controllers + validation error factory
builder.Services.AddControllers()
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

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.EnableAnnotations());
builder.Services.ConfigureOptions<ConfigureSwaggerOptions>();

// MediatR — registers all handlers from Application assembly
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(UploadVideoCommand).Assembly));

// EF Core via Aspire integration (connection string injected by Aspire AppHost)
builder.AddNpgsqlDbContext<AppDbContext>("tenniscoach");

// Infrastructure (repositories, SK kernel, video processing, background worker)
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Auto-migrate on startup (development convenience)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

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
