using AISportCoach.Application.Interfaces;
using AISportCoach.Domain.Entities;
using AISportCoach.Domain.Exceptions;
using MediatR;

namespace AISportCoach.Application.UseCases.GetVideo;

public class GetVideoHandler(
    IVideoRepository videoRepository,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetVideoQuery, VideoUpload>
{
    public async Task<VideoUpload> Handle(GetVideoQuery request, CancellationToken cancellationToken)
    {
        return await videoRepository.GetByIdAndUserAsync(request.VideoId, currentUserService.UserId, cancellationToken)
            ?? throw new VideoNotFoundException(request.VideoId);
    }
}
