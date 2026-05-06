using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AISportCoach.IntegrationTests.Integration;

/// <summary>
/// Calls the Gemini REST API directly (no Semantic Kernel) to upload and analyze videos.
/// </summary>
public class GeminiDirectVideoTest
{
    private const string FileUri = "https://generativelanguage.googleapis.com/v1beta/files/7mohrco97sm5";
    private const string UploadPath = "https://generativelanguage.googleapis.com/upload/v1beta/files";
    private const string FilesPath = "https://generativelanguage.googleapis.com/v1beta";

    private static (string apiKey, string modelId) ReadConfig()
    {
        string? apiKey = Environment.GetEnvironmentVariable("Gemini__ApiKey");
        string? modelId = Environment.GetEnvironmentVariable("Gemini__ModelId");

        foreach (var fileName in new[] { "secrets.json", "appsettings.test.json" })
        {
            var path = Path.Combine(AppContext.BaseDirectory, fileName);
            if (!File.Exists(path)) continue;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("Gemini", out var gemini)) continue;
            if (string.IsNullOrWhiteSpace(apiKey) && gemini.TryGetProperty("ApiKey", out var key))
                apiKey = key.GetString();
            if (string.IsNullOrWhiteSpace(modelId) && gemini.TryGetProperty("ModelId", out var model))
                modelId = model.GetString();
        }

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Gemini:ApiKey not set.");

        return (apiKey, modelId ?? "gemini-3-flash-preview");
    }

    private static async Task<bool> IsFileActiveAsync(HttpClient http, string fileUri, string apiKey)
    {
        var fileName = fileUri.Split('/').TakeLast(2).Aggregate((a, b) => $"{a}/{b}");
        var response = await http.GetAsync($"{FilesPath}/{fileName}?key={apiKey}");
        if (!response.IsSuccessStatusCode) return false;
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("state", out var state) &&
               state.GetString() == "ACTIVE";
    }

    private static async Task<string> UploadVideoAsync(HttpClient http, string apiKey, byte[] videoBytes, string displayName, string mimeType)
    {
        // Step 1: Initiate resumable upload
        var initiateRequest = new HttpRequestMessage(HttpMethod.Post, $"{UploadPath}?uploadType=resumable&key={apiKey}");
        var metadata = JsonSerializer.Serialize(new { file = new { display_name = displayName } });
        initiateRequest.Content = new StringContent(metadata, Encoding.UTF8, "application/json");
        initiateRequest.Headers.Add("X-Goog-Upload-Protocol", "resumable");
        initiateRequest.Headers.Add("X-Goog-Upload-Command", "start");
        initiateRequest.Headers.Add("X-Goog-Upload-Header-Content-Length", videoBytes.Length.ToString());
        initiateRequest.Headers.Add("X-Goog-Upload-Header-Content-Type", mimeType);

        var initiateResponse = await http.SendAsync(initiateRequest);
        initiateResponse.EnsureSuccessStatusCode();
        var uploadUrl = initiateResponse.Headers.GetValues("X-Goog-Upload-URL").First();

        // Step 2: Upload file bytes
        var uploadRequest = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
        uploadRequest.Content = new ByteArrayContent(videoBytes);
        uploadRequest.Content.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
        uploadRequest.Headers.Add("X-Goog-Upload-Offset", "0");
        uploadRequest.Headers.Add("X-Goog-Upload-Command", "upload, finalize");

        var uploadResponse = await http.SendAsync(uploadRequest);
        uploadResponse.EnsureSuccessStatusCode();

        var uploadResult = await uploadResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(uploadResult);
        return doc.RootElement.GetProperty("file").GetProperty("uri").GetString()!;
    }

    private static async Task WaitForFileActiveAsync(HttpClient http, string fileUri, string apiKey, int maxAttempts = 40)
    {
        for (var i = 0; i < maxAttempts; i++)
        {
            var fileName = fileUri.Split('/').TakeLast(2).Aggregate((a, b) => $"{a}/{b}");
            var response = await http.GetStringAsync($"{FilesPath}/{fileName}?key={apiKey}");
            using var doc = JsonDocument.Parse(response);
            var state = doc.RootElement.GetProperty("state").GetString();

            Console.WriteLine($"[Attempt {i + 1}/{maxAttempts}] File state: {state}");

            if (state == "ACTIVE") return;
            if (state == "FAILED") throw new InvalidOperationException($"File processing failed for {fileUri}");

            await Task.Delay(TimeSpan.FromSeconds(5));
        }

        throw new TimeoutException($"File {fileUri} did not become ACTIVE within {maxAttempts * 5} seconds timeout");
    }

    [Fact]
    public async Task UploadServeLeoVideo_DirectRestCall_UploadsAndBecomesActive()
    {
        var (apiKey, _) = ReadConfig();
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };

        // Use the test file from the FunctionalTests TestData directory
        const string testDataPath = @"C:\projects\AISportCoach\tests\AISportCoach.FunctionalTests\TestData\Serve-Leo.mp4";
        Assert.True(File.Exists(testDataPath), $"Test video file not found at: {testDataPath}");

        var videoBytes = await File.ReadAllBytesAsync(testDataPath);
        var displayName = "Serve-Leo.mp4";
        const string mimeType = "video/mp4";

        // Upload video
        var fileUri = await UploadVideoAsync(http, apiKey, videoBytes, displayName, mimeType);
        Assert.False(string.IsNullOrWhiteSpace(fileUri), "File URI should not be empty");
        Assert.StartsWith("https://generativelanguage.googleapis.com/v1beta/files/", fileUri);

        // Wait for file to become ACTIVE
        await WaitForFileActiveAsync(http, fileUri, apiKey);

        // Verify file is ACTIVE
        var isActive = await IsFileActiveAsync(http, fileUri, apiKey);
        Assert.True(isActive, $"File should be ACTIVE after upload: {fileUri}");
    }

    [Fact]
    public async Task AnalyzeVideo_DirectRestCall_ReturnsObservations()
    {
        var (apiKey, modelId) = ReadConfig();
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

        var isActive = await IsFileActiveAsync(http, FileUri, apiKey);
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
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelId}:generateContent?key={apiKey}";

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
