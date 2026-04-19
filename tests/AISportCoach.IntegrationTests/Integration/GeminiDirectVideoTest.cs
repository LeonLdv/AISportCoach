using System.Text;
using System.Text.Json;

namespace AISportCoach.IntegrationTests.Integration;

/// <summary>
/// Calls the Gemini REST API directly (no Semantic Kernel) to analyze an already-uploaded video.
/// </summary>
public class GeminiDirectVideoTest
{
    private const string Model = "gemini-2.5-flash";
    private const string FileUri = "https://generativelanguage.googleapis.com/v1beta/files/s70s6scz1uim";

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

    private static async Task<bool> IsFileActiveAsync(HttpClient http, string apiKey)
    {
        var fileName = FileUri.Split('/').TakeLast(2).Aggregate((a, b) => $"{a}/{b}");
        var response = await http.GetAsync(
            $"https://generativelanguage.googleapis.com/v1beta/{fileName}?key={apiKey}");
        if (!response.IsSuccessStatusCode) return false;
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("state", out var state) &&
               state.GetString() == "ACTIVE";
    }

    [Fact]
    public async Task AnalyzeVideo_DirectRestCall_ReturnsObservations()
    {
        var apiKey = ReadApiKey();
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

        var isActive = await IsFileActiveAsync(http, apiKey);
        Assert.True(isActive, $"Gemini file has expired or is not ACTIVE: {FileUri}. Re-upload the video first.");

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new
                        {
                            file_data = new
                            {
                                mime_type = "video/mp4",
                                file_uri = FileUri
                            }
                        },
                        new
                        {
                            text = """
                                Analyze this tennis video and return a JSON array of observations.
                                Each observation must have:
                                - stroke: one of Forehand, Backhand, Serve, Volley, Overhead, Footwork, General
                                - description: specific technique issue
                                - severity: Info, Warning, or Critical
                                - frameTimestamp: e.g. "0:05"
                                Return ONLY a valid JSON array. No other text.
                                """
                        }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent?key={apiKey}";

        HttpResponseMessage response;
        string body;
        var attempts = 0;
        do
        {
            if (attempts > 0) await Task.Delay(TimeSpan.FromSeconds(10 * attempts));
            response = await http.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
            body = await response.Content.ReadAsStringAsync();
            attempts++;
        }
        while ((int)response.StatusCode == 503 && attempts < 4);

        Assert.True(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}: {body}");

        using var doc = JsonDocument.Parse(body);
        var text = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        Assert.NotNull(text);
        Assert.NotEmpty(text);

        // Strip markdown fences if present and verify it's a JSON array
        var start = text.IndexOf('[');
        var end = text.LastIndexOf(']');
        Assert.True(start >= 0 && end > start, $"Expected JSON array in response. Got: {text}");

        var arrayJson = text[start..(end + 1)];
        using var arrayDoc = JsonDocument.Parse(arrayJson);
        Assert.Equal(JsonValueKind.Array, arrayDoc.RootElement.ValueKind);
        Assert.True(arrayDoc.RootElement.GetArrayLength() > 0, "Expected at least one observation");
    }
}
