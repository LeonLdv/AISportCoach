using AISportCoach.Application.Agents;
using AISportCoach.Application.Interfaces;
using AISportCoach.Domain.Entities;
using AISportCoach.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AISportCoach.Application.UseCases.AnalyzeNow;

public class AnalyzeNowHandler(
    IVideoRepository videoRepository,
    TennisCoachOrchestrator orchestrator,
    ILogger<AnalyzeNowHandler> logger) : IRequestHandler<AnalyzeNowCommand, CoachingReport>
{
    public async Task<CoachingReport> Handle(AnalyzeNowCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting video analysis for VideoId={VideoId}, Scopes={Scopes}",
            request.VideoId, request.Scopes?.Count > 0 ? string.Join(", ", request.Scopes) : "All");

        var video = await videoRepository.GetByIdAsync(request.VideoId, cancellationToken)
            ?? throw new VideoNotFoundException(request.VideoId);

        logger.LogDebug("Video found: FileName={FileName}, Status={Status}, GeminiFileUri={HasUri}",
            video.OriginalFileName, video.Status, !string.IsNullOrEmpty(video.GeminiFileUri));

        video.SetStatus(VideoStatus.Processing);
        await videoRepository.UpdateAsync(video, cancellationToken);
        logger.LogInformation("Video status updated to Processing for VideoId={VideoId}", request.VideoId);

        var report = await orchestrator.ProcessAsync(request.VideoId, request.Scopes, cancellationToken);

        logger.LogInformation("Analysis completed successfully for VideoId={VideoId}, ReportId={ReportId}, OverallScore={Score}",
            request.VideoId, report.Id, report.OverallScore);

        return report;
    }
}
