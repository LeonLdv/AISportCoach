using AISportCoach.Application.Agents;
using AISportCoach.Application.Interfaces;
using AISportCoach.Domain.Entities;
using AISportCoach.Domain.Exceptions;
using MediatR;

namespace AISportCoach.Application.UseCases.AnalyzeNow;

public class AnalyzeNowHandler(
    IVideoRepository videoRepository,
    TennisCoachOrchestrator orchestrator) : IRequestHandler<AnalyzeNowCommand, CoachingReport>
{
    public async Task<CoachingReport> Handle(AnalyzeNowCommand request, CancellationToken cancellationToken)
    {
        var video = await videoRepository.GetByIdAsync(request.VideoId, cancellationToken)
            ?? throw new VideoNotFoundException(request.VideoId);

        video.SetStatus(VideoStatus.Processing);
        await videoRepository.UpdateAsync(video, cancellationToken);

        return await orchestrator.ProcessAsync(request.VideoId, cancellationToken);
    }
}
