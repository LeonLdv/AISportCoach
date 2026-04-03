using AISportCoach.Application.Interfaces;
using AISportCoach.Domain.Entities;
using AISportCoach.Domain.Exceptions;
using MediatR;

namespace AISportCoach.Application.UseCases.GetVideo;

public class GetVideoHandler(IVideoRepository videoRepository)
    : IRequestHandler<GetVideoQuery, VideoUpload>
{
    public async Task<VideoUpload> Handle(GetVideoQuery request, CancellationToken cancellationToken)
    {
        return await videoRepository.GetByIdAsync(request.VideoId, cancellationToken)
            ?? throw new VideoNotFoundException(request.VideoId);
    }
}
