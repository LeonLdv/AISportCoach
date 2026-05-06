#pragma warning disable SKEXP0001, SKEXP0070
using AISportCoach.Application.Agents;
using AISportCoach.Application.Interfaces;
using AISportCoach.Application.Options;
using AISportCoach.Application.Plugins;
using AISportCoach.Infrastructure.Database;
using AISportCoach.Infrastructure.Persistence.Repositories;
using AISportCoach.Infrastructure.Services;
using AISportCoach.Infrastructure.VideoProcessing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;

namespace AISportCoach.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Note: Identity and HttpContextAccessor are registered in API project's Program.cs
        // because AddIdentity requires ASP.NET Core framework features

        // Current user (Scoped for HttpContext access)
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // Auth services
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IEmailService, EmailService>();

        // Repositories
        services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IWebAuthnCredentialRepository, WebAuthnCredentialRepository>();
        services.AddScoped<IVideoRepository, VideoRepository>();
        services.AddScoped<ICoachingReportRepository, CoachingReportRepository>();
        services.AddScoped<IReportEmbeddingRepository, ReportEmbeddingRepository>();

        // Development seed data
        services.AddScoped<DevelopmentSeeder>();

        // Gemini options
        services.Configure<GeminiOptions>(configuration.GetSection("Gemini"));

        // RAG options
        services.Configure<RagOptions>(configuration.GetSection("Rag"));

        // JWT options
        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));

        // Video File API service (upload + active-check; DB is the URI cache).
        // Uploads can stream up to 500MB to Gemini, so the resilience pipeline
        // and HttpClient.Timeout are both extended — duration driven by Gemini:HttpTimeoutMinutes.
        var httpTimeout = TimeSpan.FromMinutes(
            configuration.GetValue<int>("Gemini:HttpTimeoutMinutes", 10));
        services.AddHttpClient<VideoFileService>()
            .ConfigureHttpClient((sp, c) =>
            {
                var opts = sp.GetRequiredService<IOptions<GeminiOptions>>().Value;
                c.BaseAddress = new Uri(opts.BaseUrl);
                c.Timeout = httpTimeout;
            })
            .AddStandardResilienceHandler(options =>
            {
                options.TotalRequestTimeout.Timeout = httpTimeout;
                options.AttemptTimeout.Timeout = httpTimeout;
                options.CircuitBreaker.SamplingDuration = httpTimeout * 2;
            });

        services.AddTransient<IVideoFileService>(sp => sp.GetRequiredService<VideoFileService>());

        // Embedding service
        services.AddScoped<IEmbeddingService, GeminiEmbeddingService>();

        // Semantic Kernel
        services.AddSemanticKernel(configuration);

        return services;
    }

    private static void AddSemanticKernel(this IServiceCollection services, IConfiguration configuration)
    {
        // Plugins are called directly by the orchestrator — no agent/function-calling involved
        services.AddScoped<VideoAnalysisPlugin>();
        services.AddScoped<ReportGenerationPlugin>();
        services.AddScoped<CoachQAPlugin>();
        services.AddScoped<TennisCoachOrchestrator>();

        services.AddGoogleAIEmbeddingGenerator(
            modelId: "gemini-embedding-001",
            apiKey: configuration["Gemini:ApiKey"]!,
            apiVersion: GoogleAIVersion.V1_Beta,
            dimensions: 768);

        services.AddScoped<Kernel>(sp =>
        {
            var kernelBuilder = Kernel.CreateBuilder();

            var geminiOpts = sp.GetRequiredService<IOptions<GeminiOptions>>().Value;

            if (string.IsNullOrEmpty(geminiOpts.ApiKey))
                throw new InvalidOperationException("Gemini:ApiKey is not configured");
            if (string.IsNullOrEmpty(geminiOpts.ModelId))
                throw new InvalidOperationException("Gemini:ModelId is not configured");

            kernelBuilder.AddGoogleAIGeminiChatCompletion(modelId: geminiOpts.ModelId, apiKey: geminiOpts.ApiKey);

            return kernelBuilder.Build();
        });
    }
}
#pragma warning restore SKEXP0001, SKEXP0070
