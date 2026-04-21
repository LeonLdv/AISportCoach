#pragma warning disable SKEXP0070

using System.Text.Json;
using AISportCoach.Application.Interfaces;
using AISportCoach.Infrastructure.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.Connectors.Google;

namespace AISportCoach.IntegrationTests.Integration;

public class EmbeddingServiceTest
{
    private const string Text =
        "Player struggles with footwork and body rotation across all strokes. " +
        "Forehand: late contact, open racket face, insufficient shoulder turn, no wrist lag. " +
        "Backhand: shortened backswing, contact too close to body, truncated follow-through. " +
        "Serve: inconsistent ball toss, underdeveloped trophy pose. " +
        "General: poor split-step timing, insufficient knee bend, poor dynamic balance.";

    private static string ReadApiKey()
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

    [Fact]
    public async Task GenerateEmbeddingAsync_ValidText_Returns768DimVector()
    {
        var apiKey = ReadApiKey();

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

#pragma warning restore SKEXP0070
