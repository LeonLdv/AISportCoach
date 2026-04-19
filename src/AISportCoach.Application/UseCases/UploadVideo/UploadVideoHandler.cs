using AISportCoach.Application.Interfaces;
using AISportCoach.Domain.Constants;
using AISportCoach.Domain.Entities;
using AISportCoach.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace AISportCoach.Application.UseCases.UploadVideo;

public class UploadVideoHandler(
    IVideoFileService videoFileService,
    IVideoRepository videoRepository,
    IConfiguration configuration) : IRequestHandler<UploadVideoCommand, UploadVideoResult>
{
    public async Task<UploadVideoResult> Handle(UploadVideoCommand request, CancellationToken cancellationToken)
    {
        var maxSizeMb = configuration.GetValue<long>("VideoStorage:MaxFileSizeMB", 500);
        var maxSizeBytes = maxSizeMb * 1024 * 1024;
        var allowedExtensions = configuration.GetSection("VideoStorage:AllowedExtensions").Get<string[]>()
            ?? [".mp4", ".mov", ".avi", ".mkv"];

        if (request.FileSizeBytes > maxSizeBytes)
            throw new VideoTooLargeException(request.FileSizeBytes, maxSizeBytes);

        var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
            throw new UnsupportedVideoFormatException(extension);

        var geminiFileUri = await videoFileService.UploadVideoStreamAsync(request.FileStream, request.FileName, cancellationToken);
        var video = VideoUpload.Create(request.FileName, request.FileSizeBytes, geminiFileUri, MockUser.Id);

        await videoRepository.AddAsync(video, cancellationToken);

        return new UploadVideoResult(video.Id, video.OriginalFileName, video.FileSizeBytes,
            video.Status.ToString(), video.CreatedAt);
    }
}
