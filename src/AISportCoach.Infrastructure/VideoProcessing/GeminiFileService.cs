using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AISportCoach.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AISportCoach.Infrastructure.VideoProcessing;

public class VideoFileService(
    IOptions<GeminiOptions> options,
    ILogger<VideoFileService> logger,
    HttpClient http) : IVideoFileService
{
    private const string UploadPath = "/upload/v1beta/files";
    private const string FilesPath = "/v1beta/";

    private readonly string _apiKey = options.Value.ApiKey is { Length: > 0 } apiKey
        ? apiKey
        : throw new InvalidOperationException("Gemini:ApiKey is not configured");

    public async Task<string> UploadVideoStreamAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        logger.LogInformation("Uploading video stream to AI File API: {FileName}", fileName);

        using var ms = new System.IO.MemoryStream();
        await stream.CopyToAsync(ms, ct);
        var fileBytes = ms.ToArray();
        var displayName = Path.GetFileName(fileName);
        var mimeType = Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".mov" => "video/quicktime",
            ".avi" => "video/x-msvideo",
            ".mkv" => "video/x-matroska",
            _ => "video/mp4"
        };

        return await UploadBytesAsync(fileBytes, displayName, mimeType, ct);
    }

    public async Task<string> UploadVideoAsync(string videoPath, CancellationToken ct = default)
    {
        logger.LogInformation("Uploading video to AI File API: {VideoPath}", videoPath);

        var fileBytes = await File.ReadAllBytesAsync(videoPath, ct);
        var displayName = Path.GetFileName(videoPath);
        const string mimeType = "video/mp4";

        return await UploadBytesAsync(fileBytes, displayName, mimeType, ct);
    }

    private async Task<string> UploadBytesAsync(byte[] fileBytes, string displayName, string mimeType, CancellationToken ct)
    {
        // Step 1 — initiate resumable upload
        var initiateRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{UploadPath}?uploadType=resumable&key={_apiKey}");

        var metadata = JsonSerializer.Serialize(new { file = new { display_name = displayName } });
        initiateRequest.Content = new StringContent(metadata, Encoding.UTF8, "application/json");
        initiateRequest.Headers.Add("X-Goog-Upload-Protocol", "resumable");
        initiateRequest.Headers.Add("X-Goog-Upload-Command", "start");
        initiateRequest.Headers.Add("X-Goog-Upload-Header-Content-Length", fileBytes.Length.ToString());
        initiateRequest.Headers.Add("X-Goog-Upload-Header-Content-Type", mimeType);

        var initiateResponse = await http.SendAsync(initiateRequest, ct);
        initiateResponse.EnsureSuccessStatusCode();

        var uploadUrl = initiateResponse.Headers.GetValues("X-Goog-Upload-URL").First();

        // Step 2 — upload file bytes
        var uploadRequest = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
        uploadRequest.Content = new ByteArrayContent(fileBytes);
        uploadRequest.Content.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
        uploadRequest.Headers.Add("X-Goog-Upload-Offset", "0");
        uploadRequest.Headers.Add("X-Goog-Upload-Command", "upload, finalize");

        var uploadResponse = await http.SendAsync(uploadRequest, ct);
        uploadResponse.EnsureSuccessStatusCode();

        var uploadResult = await uploadResponse.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(uploadResult);
        var fileUri = doc.RootElement.GetProperty("file").GetProperty("uri").GetString()!;

        logger.LogInformation("Video uploaded. File URI: {FileUri}", fileUri);

        await WaitForFileActiveAsync(fileUri, ct);
        return fileUri;
    }

    public async Task<bool> IsFileActiveAsync(string fileUri, CancellationToken ct = default)
    {
        try
        {
            var fileName = fileUri.Split('/').TakeLast(2).Aggregate((a, b) => $"{a}/{b}");
            var response = await http.GetAsync(
                $"{FilesPath}{fileName}?key={_apiKey}", ct);
            if (!response.IsSuccessStatusCode) return false;

            var body = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("state").GetString() == "ACTIVE";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not verify file status for {FileUri}", fileUri);
            return false;
        }
    }

    private async Task WaitForFileActiveAsync(string fileUri, CancellationToken ct)
    {
        var fileName = fileUri.Split('/').TakeLast(2).Aggregate((a, b) => $"{a}/{b}");

        for (var i = 0; i < 20; i++)
        {
            ct.ThrowIfCancellationRequested();
            var response = await http.GetStringAsync(
                $"{FilesPath}{fileName}?key={_apiKey}", ct);

            using var doc = JsonDocument.Parse(response);
            var state = doc.RootElement.GetProperty("state").GetString();

            if (state == "ACTIVE") return;
            if (state == "FAILED") throw new InvalidOperationException($"AI file processing failed for {fileUri}.");

            logger.LogDebug("File {FileUri} state: {State}. Waiting...", fileUri, state);
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }

        throw new TimeoutException($"File {fileUri} did not become ACTIVE within timeout.");
    }
}
