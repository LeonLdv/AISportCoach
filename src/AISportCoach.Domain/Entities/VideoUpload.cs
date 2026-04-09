namespace AISportCoach.Domain.Entities;
public class VideoUpload
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string OriginalFileName { get; private set; } = string.Empty;
    public long FileSizeBytes { get; private set; }
    public VideoStatus Status { get; private set; }
    public DateTime UploadedAt { get; private set; }
    public string? GeminiFileUri { get; private set; }

    private VideoUpload() { }

    public static VideoUpload Create(string originalFileName, long fileSizeBytes, string geminiFileUri, Guid userId)
    {
        return new VideoUpload
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            OriginalFileName = originalFileName,
            FileSizeBytes = fileSizeBytes,
            GeminiFileUri = geminiFileUri,
            Status = VideoStatus.Uploaded,
            UploadedAt = DateTime.UtcNow
        };
    }

    public void SetStatus(VideoStatus status) => Status = status;
    public void SetGeminiFileUri(string uri) => GeminiFileUri = uri;
}
