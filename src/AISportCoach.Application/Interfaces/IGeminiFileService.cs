namespace AISportCoach.Application.Interfaces;

public interface IVideoFileService
{
    Task<string> UploadVideoAsync(string videoPath, CancellationToken ct = default);
    Task<string> UploadVideoStreamAsync(Stream stream, string fileName, CancellationToken ct = default);
    Task<bool> IsFileActiveAsync(string fileUri, CancellationToken ct = default);
}
