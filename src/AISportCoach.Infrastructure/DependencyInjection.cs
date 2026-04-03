using AISportCoach.Application.Agents;
using AISportCoach.Application.Interfaces;
using AISportCoach.Application.Plugins;
using AISportCoach.Infrastructure.Persistence;
using AISportCoach.Infrastructure.Persistence.Repositories;
using AISportCoach.Infrastructure.VideoProcessing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

namespace AISportCoach.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Repositories
        services.AddScoped<IVideoRepository, VideoRepository>();
        services.AddScoped<ICoachingReportRepository, CoachingReportRepository>();

        // Video File API service (upload + active-check; DB is the URI cache)
        services.AddHttpClient<VideoFileService>()
            .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromMinutes(10));
        services.AddSingleton<IVideoFileService, VideoFileService>();

        // Semantic Kernel
        services.AddSemanticKernel(configuration);

        return services;
    }

    private static void AddSemanticKernel(this IServiceCollection services, IConfiguration configuration)
    {
        // Plugins are called directly by the orchestrator — no agent/function-calling involved
        services.AddScoped<VideoAnalysisPlugin>();
        services.AddScoped<ReportGenerationPlugin>();
        services.AddScoped<NtrpRatingPlugin>();
        services.AddScoped<TennisCoachOrchestrator>();

        services.AddScoped<Kernel>(sp =>
        {
            var kernelBuilder = Kernel.CreateBuilder();

            // Retry with exponential backoff for free-tier 429 rate limits
            // kernelBuilder.Services.ConfigureHttpClientDefaults(b => b.AddStandardResilienceHandler(o =>
            // {
            //     o.Retry.MaxRetryAttempts = 2;
            //     o.Retry.Delay = TimeSpan.FromSeconds(5);
            //     o.Retry.UseJitter = true;
            //     o.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(2);
            // }));

            var geminiKey = configuration["Gemini:ApiKey"]
                ?? throw new InvalidOperationException("Gemini:ApiKey is not configured");
            var geminiModel = configuration["Gemini:ModelId"]
                ?? throw new InvalidOperationException("Gemini:ModelId is not configured");
            kernelBuilder.AddGoogleAIGeminiChatCompletion(modelId: geminiModel, apiKey: geminiKey);

            return kernelBuilder.Build();
        });
    }
}
