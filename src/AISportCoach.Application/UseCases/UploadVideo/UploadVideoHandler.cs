using AISportCoach.Application.Interfaces;
using AISportCoach.Domain.Constants;
using AISportCoach.Domain.Entities;
using AISportCoach.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AISportCoach.Application.UseCases.UploadVideo;

public class UploadVideoHandler(
    IVideoFileService videoFileService,
    IVideoRepository videoRepository,
    IConfiguration configuration,
    ILogger<UploadVideoHandler> logger) : IRequestHandler<UploadVideoCommand, UploadVideoResult>
{
    public async Task<UploadVideoResult> Handle(UploadVideoCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting video upload: FileName={FileName}, Size={SizeKB}KB",
            request.FileName, request.FileSizeBytes / 1024.0);

        var maxSizeMb = configuration.GetValue<long>("VideoStorage:MaxFileSizeMB", 500);
        var maxSizeBytes = maxSizeMb * 1024 * 1024;
        var allowedExtensions = configuration.GetSection("VideoStorage:AllowedExtensions").Get<string[]>()
            ?? [".mp4", ".mov", ".avi", ".mkv"];

        logger.LogDebug("Video validation: MaxSize={MaxSizeMB}MB, AllowedExtensions={Extensions}",
            maxSizeMb, string.Join(", ", allowedExtensions));

        if (request.FileSizeBytes > maxSizeBytes)
        {
            logger.LogWarning("Video upload rejected: File too large. Size={SizeBytes}, Max={MaxBytes}",
                request.FileSizeBytes, maxSizeBytes);
            throw new VideoTooLargeException(request.FileSizeBytes, maxSizeBytes);
        }

        var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
        {
            logger.LogWarning("Video upload rejected: Unsupported format. Extension={Extension}", extension);
            throw new UnsupportedVideoFormatException(extension);
        }

        logger.LogInformation("Uploading video to Gemini File API: {FileName}", request.FileName);
        var geminiFileUri = await videoFileService.UploadVideoStreamAsync(request.FileStream, request.FileName, cancellationToken);
        logger.LogInformation("Video uploaded to Gemini successfully: Uri={GeminiFileUri}", geminiFileUri);

        var video = VideoUpload.Create(request.FileName, request.FileSizeBytes, geminiFileUri, MockUser.Id);

        await videoRepository.AddAsync(video, cancellationToken);
        logger.LogInformation("Video upload completed successfully: VideoId={VideoId}, Status={Status}",
            video.Id, video.Status);

        return new UploadVideoResult(video.Id, video.OriginalFileName, video.FileSizeBytes,
            video.Status.ToString(), video.CreatedAt);
    }
}
