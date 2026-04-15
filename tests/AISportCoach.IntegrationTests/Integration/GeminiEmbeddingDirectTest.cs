using System.Net.Http.Json;
using System.Text.Json;

namespace AISportCoach.IntegrationTests.Integration;

public class GeminiEmbeddingDirectTest
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/";
    private const string Model   = "gemini-embedding-001";

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

    private static HttpClient BuildClient(string apiKey) =>
        new(new HttpClientHandler()) { BaseAddress = new Uri(BaseUrl),
            DefaultRequestHeaders = { { "x-goog-api-key", apiKey } } };

    [Fact]
    public async Task ListModels_ShowsAvailableEmbeddingModels()
    {
        using var http = BuildClient(ReadApiKey());

        var response = await http.GetAsync("models?pageSize=200");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}: {body}");

        using var doc = JsonDocument.Parse(body);
        var embeddingModels = doc.RootElement
            .GetProperty("models")
            .EnumerateArray()
            .Where(m => m.TryGetProperty("supportedGenerationMethods", out var methods) &&
                        methods.EnumerateArray().Any(v => v.GetString() == "embedContent"))
            .Select(m => m.GetProperty("name").GetString())
            .ToList();

        Assert.True(embeddingModels.Count > 0,
            $"No embedding models found. Full response: {body}");
    }

    [Fact]
    public async Task EmbedContent_ValidText_Returns768DimVector()
    {
        using var http = BuildClient(ReadApiKey());

        var requestBody = new
        {
            model = $"models/{Model}",
            content = new { parts = new[] { new { text = Text } } },
            taskType = "SEMANTIC_SIMILARITY",
            outputDimensionality = 768
        };

        var response = await http.PostAsJsonAsync(
            $"models/{Model}:embedContent",
            requestBody);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}: {body}");

        using var doc = JsonDocument.Parse(body);
        var values = doc.RootElement
            .GetProperty("embedding")
            .GetProperty("values")
            .EnumerateArray()
            .Select(v => v.GetSingle())
            .ToArray();

        Assert.Equal(768, values.Length);
        Assert.Contains(values, v => v != 0f);
    }
}
