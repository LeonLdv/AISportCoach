using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using AISportCoach.FunctionalTests.Fixtures;

namespace AISportCoach.FunctionalTests;

[Collection(AspireCollection.Name)]
public class VideoAuthorizationTests(AspireFixture fixture)
{
    private readonly HttpClient _unauthenticatedClient = fixture.ApiClient;
    private const string UploadUrl = "/api/v1/videos";
    private static readonly string TestVideoPath = Path.Combine(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
        "TestData",
        "sample.mp4"
    );

    [Fact]
    public async Task Upload_WithoutAuthentication_Returns401()
    {
        using var content = new MultipartFormDataContent();
        await using var stream = File.OpenRead(TestVideoPath);
        var streamContent = new StreamContent(stream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        content.Add(streamContent, "file", "test.mp4");

        var response = await _unauthenticatedClient.PostAsync(UploadUrl, content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetById_WithoutAuthentication_Returns401()
    {
        var videoId = Guid.NewGuid();
        var response = await _unauthenticatedClient.GetAsync($"{UploadUrl}/{videoId}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Analyze_WithoutAuthentication_Returns401()
    {
        var videoId = Guid.NewGuid();
        var response = await _unauthenticatedClient.PostAsync($"{UploadUrl}/{videoId}/analyze", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Upload_WithInvalidToken_Returns401()
    {
        var client = fixture.ApiClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token-12345");

        using var content = new MultipartFormDataContent();
        await using var stream = File.OpenRead(TestVideoPath);
        var streamContent = new StreamContent(stream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        content.Add(streamContent, "file", "test.mp4");

        var response = await client.PostAsync(UploadUrl, content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        // Clean up auth header to avoid affecting other tests
        client.DefaultRequestHeaders.Authorization = null;
    }
}
