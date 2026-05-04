using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using AISportCoach.FunctionalTests.Fixtures;
using Xunit.Abstractions;

namespace AISportCoach.FunctionalTests;

/// <summary>
/// Functional tests for video analysis using the real Gemini API.
///
/// SETUP REQUIRED:
/// These tests require a valid Gemini API key configured in user secrets for the API project.
///
/// To configure:
///   dotnet user-secrets set "Gemini:ApiKey" "YOUR-API-KEY" --project src/AISportCoach.API
///
/// NOTE: This test is slow (30-120 seconds) and uses real Gemini API quota.
/// Run manually when validating Gemini integration.
/// </summary>
[Collection(AspireCollection.Name)]
public class VideoAnalysisTests(AspireFixture fixture, ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;
    private const string UploadUrl = "/api/v1/videos";
    private static readonly string TestVideoPath = Path.Combine(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
        "TestData",
        "sample.mp4"
    );

    [Fact]
    public async Task UploadAndAnalyze_WithRealGemini_ReturnsCoachingReport()
    {
        var client = await fixture.AuthHelper.GetDefaultAuthenticatedClientAsync();

        var sw = Stopwatch.StartNew();
        _output.WriteLine("=== Upload and Analyze E2E Test (Real Gemini API) ===");

        // Arrange: Upload video (uploads to real Gemini via VideoFileService)
        _output.WriteLine("Phase 1: Uploading test video to Gemini...");
        var videoId = await UploadTestVideoAsync(client, "gemini-analysis-test.mp4");
        _output.WriteLine($"✓ Video uploaded successfully. VideoId={videoId}");

        // Act: Analyze the uploaded video
        _output.WriteLine("Phase 2: Requesting video analysis...");
        var analyzeUrl = $"{UploadUrl}/{videoId}/analyze";
        _output.WriteLine($"  POST {analyzeUrl}");
        var response = await client.PostAsync(analyzeUrl, null);
        _output.WriteLine($"✓ Analysis response received: {response.StatusCode}");

        // Assert: Verify response structure
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        // Basic structure
        var reportId = root.GetProperty("id").GetString();
        Assert.True(Guid.TryParse(reportId, out _));

        var overallScore = root.GetProperty("overallScore").GetInt32();
        Assert.InRange(overallScore, 1, 100);

        var executiveSummary = root.GetProperty("executiveSummary").GetString();
        Assert.NotNull(executiveSummary);
        Assert.NotEmpty(executiveSummary);

        _output.WriteLine($"✓ Report created: ReportId={reportId}, OverallScore={overallScore}");
        _output.WriteLine($"  Executive Summary: {executiveSummary[..Math.Min(150, executiveSummary.Length)]}...");

        // NTRP rating (optional, but if present must be valid)
        if (root.TryGetProperty("ntrpRating", out var ntrpRating) &&
            ntrpRating.ValueKind != JsonValueKind.Null)
        {
            var rating = ntrpRating.GetDouble();
            Assert.InRange(rating, 1.5, 7.0);
            _output.WriteLine($"✓ NTRP Rating: {rating}");
        }
        else
        {
            _output.WriteLine("  NTRP Rating: Not present");
        }

        // Observations and recommendations
        var observations = root.GetProperty("observations");
        Assert.Equal(JsonValueKind.Array, observations.ValueKind);
        Assert.True(observations.GetArrayLength() > 0,
            "Expected at least one observation from video analysis");

        var recommendations = root.GetProperty("recommendations");
        Assert.Equal(JsonValueKind.Array, recommendations.ValueKind);
        Assert.True(recommendations.GetArrayLength() > 0,
            "Expected at least one recommendation");

        _output.WriteLine($"✓ Observations: {observations.GetArrayLength()}, Recommendations: {recommendations.GetArrayLength()}");

        // Location header
        Assert.NotNull(response.Headers.Location);
        Assert.Contains("/api/v1/reports/", response.Headers.Location.ToString());
        _output.WriteLine($"✓ Location header: {response.Headers.Location}");

        sw.Stop();
        _output.WriteLine($"=== Test completed in {sw.ElapsedMilliseconds}ms ({sw.Elapsed:mm\\:ss}) ===");
    }

    /// <summary>
    /// Helper method to upload a test video and return its ID.
    /// Each test should call this to create its own isolated video.
    /// </summary>
    private async Task<Guid> UploadTestVideoAsync(HttpClient client, string fileName)
    {
        var sw = Stopwatch.StartNew();
        _output.WriteLine($"  [Upload] Starting upload of {fileName}...");

        using var content = new MultipartFormDataContent();
        await using var stream = File.OpenRead(TestVideoPath);
        var fileSize = new FileInfo(TestVideoPath).Length;
        _output.WriteLine($"  [Upload] File size: {fileSize / 1024.0:F2} KB");

        var streamContent = new StreamContent(stream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        content.Add(streamContent, "file", fileName);

        var response = await client.PostAsync(UploadUrl, content);
        _output.WriteLine($"  [Upload] Response: {response.StatusCode}");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var videoIdString = doc.RootElement.GetProperty("id").GetString()!;

        sw.Stop();
        _output.WriteLine($"  [Upload] Upload completed in {sw.ElapsedMilliseconds}ms. VideoId={videoIdString}");

        return Guid.Parse(videoIdString);
    }
}
