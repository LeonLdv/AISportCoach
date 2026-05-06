using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using AISportCoach.FunctionalTests.Fixtures;

namespace AISportCoach.FunctionalTests;

[Collection(AspireCollection.Name)]
public class VideoUploadTests(AspireFixture fixture)
{
    private const string UploadUrl = "/api/v1/videos";
    private static readonly string TestDataPath = Path.Combine(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
        "TestData"
    );

    private static string GetTestVideoPath(string fileName) => Path.Combine(TestDataPath, fileName);

    [Fact]
    public async Task Upload_EmptyFile_Returns400()
    {
        var client = await fixture.AuthHelper.GetDefaultAuthenticatedClientAsync();

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        content.Add(fileContent, "file", "empty.mp4");

        var response = await client.PostAsync(UploadUrl, content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.TryGetProperty("errors", out var errors));
        Assert.True(errors.TryGetProperty("file", out _));
    }

    [Fact]
    public async Task Upload_NoFileInRequest_Returns422()
    {
        var client = await fixture.AuthHelper.GetDefaultAuthenticatedClientAsync();

        using var content = new MultipartFormDataContent();

        var response = await client.PostAsync(UploadUrl, content);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Upload_UnsupportedExtension_Returns400()
    {
        var client = await fixture.AuthHelper.GetDefaultAuthenticatedClientAsync();

        using var content = new MultipartFormDataContent();
        var fileBytes = new byte[1024];
        Random.Shared.NextBytes(fileBytes);
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", "notes.txt");

        var response = await client.PostAsync(UploadUrl, content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(400, doc.RootElement.GetProperty("status").GetInt32());
        Assert.Contains(".txt", doc.RootElement.GetProperty("detail").GetString());
    }

    [Theory]
    [InlineData("sample.mp4")]
   // [InlineData("Serve-Leo.mp4")] // lage file 800 Mb
    public async Task Upload_ValidMp4_Returns201(string fileName)
    {
        var client = await fixture.AuthHelper.GetDefaultAuthenticatedClientAsync();

        using var content = new MultipartFormDataContent();
        await using var stream = File.OpenRead(GetTestVideoPath(fileName));
        var streamContent = new StreamContent(stream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        content.Add(streamContent, "file", fileName);

        var response = await client.PostAsync(UploadUrl, content);

        if (response.StatusCode != HttpStatusCode.Created)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"Upload failed with {response.StatusCode}: {errorBody}");
        }

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        Assert.Equal(fileName, root.GetProperty("originalFileName").GetString());
        Assert.True(root.GetProperty("fileSizeBytes").GetInt64() > 0);
        Assert.Equal("Uploaded", root.GetProperty("status").GetString());
        Assert.True(Guid.TryParse(root.GetProperty("id").GetString(), out _));
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task GetById_AfterUpload_Returns200()
    {
        var client = await fixture.AuthHelper.GetDefaultAuthenticatedClientAsync();

        using var uploadContent = new MultipartFormDataContent();
        await using var stream = File.OpenRead(GetTestVideoPath("sample.mp4"));
        var streamContent = new StreamContent(stream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        uploadContent.Add(streamContent, "file", "retrieve-test.mp4");

        var uploadResponse = await client.PostAsync(UploadUrl, uploadContent);

        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);

        var uploadBody = await uploadResponse.Content.ReadAsStringAsync();
        using var uploadDoc = JsonDocument.Parse(uploadBody);
        var videoId = uploadDoc.RootElement.GetProperty("id").GetString();

        var getResponse = await client.GetAsync($"{UploadUrl}/{videoId}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var getBody = await getResponse.Content.ReadAsStringAsync();
        using var getDoc = JsonDocument.Parse(getBody);
        Assert.Equal(videoId, getDoc.RootElement.GetProperty("id").GetString());
        Assert.Equal("retrieve-test.mp4", getDoc.RootElement.GetProperty("originalFileName").GetString());
    }

    [Fact]
    public async Task GetById_NonExistentId_Returns404()
    {
        var client = await fixture.AuthHelper.GetDefaultAuthenticatedClientAsync();

        var response = await client.GetAsync($"{UploadUrl}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
