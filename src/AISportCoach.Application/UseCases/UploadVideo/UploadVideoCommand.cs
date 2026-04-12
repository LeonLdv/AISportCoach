using MediatR;
namespace AISportCoach.Application.UseCases.UploadVideo;

public record UploadVideoCommand(
    Stream FileStream,
    string FileName,
    long FileSizeBytes) : IRequest<UploadVideoResult>;

public record UploadVideoResult(
    Guid Id,
    string OriginalFileName,
    long FileSizeBytes,
    string Status,
    DateTime UploadedAt);
