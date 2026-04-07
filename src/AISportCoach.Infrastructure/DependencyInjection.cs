using AISportCoach.Application.Agents;
using AISportCoach.Application.Interfaces;
using AISportCoach.Application.Plugins;
using AISportCoach.Infrastructure.Persistence.Repositories;
using AISportCoach.Infrastructure.VideoProcessing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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

        // Gemini options
        services.Configure<GeminiOptions>(configuration.GetSection("Gemini"));

        // Video File API service (upload + active-check; DB is the URI cache)
        // AddHttpClient registers VideoFileService as transient and injects the configured HttpClient.
        // The interface is resolved via a factory so the same IHttpClientFactory-managed instance is used.
        services.AddHttpClient<VideoFileService>()
            .ConfigureHttpClient((sp, c) =>
            {
                var opts = sp.GetRequiredService<IOptions<GeminiOptions>>().Value;
                c.BaseAddress = new Uri(opts.BaseUrl);
                c.Timeout = TimeSpan.FromMinutes(10);
            });
        services.AddTransient<IVideoFileService>(sp => sp.GetRequiredService<VideoFileService>());

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
