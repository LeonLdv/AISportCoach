#pragma warning disable SKEXP0070

using System.Text.Json;
using AISportCoach.Application.Interfaces;
using AISportCoach.Application.Services;
using AISportCoach.Domain.Entities;
using AISportCoach.Domain.Enums;
using AISportCoach.Infrastructure.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.Connectors.Google;

namespace AISportCoach.IntegrationTests.Integration;

internal static class GeminiTestHelper
{
    internal static string ReadApiKey()
    {
        var fromEnv = Environment.GetEnvironmentVariable("Gemini__ApiKey");
        if (!string.IsNullOrWhiteSpace(fromEnv)) return fromEnv;

        foreach (var fileName in new[] { "secrets.json", "appsettings.test.json" })
        {
            var path = Path.Combine(AppContext.BaseDirectory, fileName);
            if (!File.Exists(path)) continue;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("Gemini", out var gemini) &&
                gemini.TryGetProperty("ApiKey", out var key))
            {
                var value = key.GetString();
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
        }

        throw new InvalidOperationException("Gemini:ApiKey not set.");
    }
}

public class EmbeddingServiceTest
{
    private const string Text =
        "Player struggles with footwork and body rotation across all strokes. " +
        "Forehand: late contact, open racket face, insufficient shoulder turn, no wrist lag. " +
        "Backhand: shortened backswing, contact too close to body, truncated follow-through. " +
        "Serve: inconsistent ball toss, underdeveloped trophy pose. " +
        "General: poor split-step timing, insufficient knee bend, poor dynamic balance.";

    [Fact]
    public async Task GenerateEmbeddingAsync_ValidText_Returns768DimVector()
    {
        var apiKey = GeminiTestHelper.ReadApiKey();

        var services = new ServiceCollection();
        services.AddGoogleAIEmbeddingGenerator(
            modelId: "gemini-embedding-001",
            apiKey: apiKey,
            apiVersion: GoogleAIVersion.V1_Beta,
            dimensions: 768);

        var serviceProvider = services.BuildServiceProvider();
        var embeddingGenerator = serviceProvider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        var service = new GeminiEmbeddingService(embeddingGenerator);

        var vector = await service.GenerateEmbeddingAsync(Text, EmbeddingTaskType.Document, CancellationToken.None);

        Assert.Equal(768, vector.Length);
        Assert.Contains(vector, v => v != 0f);
    }
}

public class ReportChunkingIntegrationTest
{
    [Fact]
    public async Task ChunkAndEmbed_ReportWith2ObsAnd1Rec_Returns4VectorsOf768Dims()
    {
        var apiKey = GeminiTestHelper.ReadApiKey();

        var services = new ServiceCollection();
        services.AddGoogleAIEmbeddingGenerator(
            modelId: "gemini-embedding-001",
            apiKey: apiKey,
            apiVersion: GoogleAIVersion.V1_Beta,
            dimensions: 768);

        var serviceProvider = services.BuildServiceProvider();
        var embeddingGenerator = serviceProvider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        var embeddingService = new GeminiEmbeddingService(embeddingGenerator);
        var chunker = new ReportChunker(NullLogger<ReportChunker>.Instance);

        var observations = new List<TechniqueObservation>
        {
            new() { Id = Guid.CreateVersion7(), Stroke = TennisStroke.Forehand, Severity = SeverityLevel.Warning,
                    Description = "Late contact point, racket face too open at impact.",
                    FrameTimestamp = "00:01:05", BodyPart = "Wrist" },
            new() { Id = Guid.CreateVersion7(), Stroke = TennisStroke.Backhand, Severity = SeverityLevel.Critical,
                    Description = "Shortened backswing with early deceleration.",
                    FrameTimestamp = "00:02:15", BodyPart = "Shoulder" }
        };
        var recommendations = new List<ImprovementRecommendation>
        {
            new() { Id = Guid.CreateVersion7(), Title = "Improve topspin", Priority = 1,
                    TargetStroke = TennisStroke.Forehand,
                    DetailedDescription = "Focus on low-to-high swing path.",
                    DrillSuggestions = ["Shadow swing", "Slow motion practice"] }
        };
        var report = CoachingReport.Create(
            Guid.CreateVersion7(), 65,
            "Player shows inconsistency across groundstrokes due to poor swing mechanics.",
            observations, recommendations);

        var chunks = chunker.Chunk(report);
        // 1 summary + 2 observations + 1 recommendation = 4
        Assert.Equal(4, chunks.Count);

        foreach (var chunk in chunks)
        {
            var vector = await embeddingService.GenerateEmbeddingAsync(
                chunk.Text, EmbeddingTaskType.Document, CancellationToken.None);

            Assert.Equal(768, vector.Length);
            Assert.Contains(vector, v => v != 0f);
        }
    }
}

#pragma warning restore SKEXP0070
