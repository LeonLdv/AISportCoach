using AISportCoach.Application.Agents;
using AISportCoach.Application.Interfaces;
using AISportCoach.Application.Options;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AISportCoach.Application.UseCases.AskCoach;

public class CoachAskHandler(
    IEmbeddingService embeddingService,
    IReportEmbeddingRepository embeddingRepository,
    ICurrentUserService currentUserService,
    IOptions<RagOptions> ragOptions,
    TennisCoachAdvisorOrchestrator tennisCoachAdvisorOrchestrator,
    ILogger<CoachAskHandler> logger) : IRequestHandler<CoachAskQuery, string>
{
    public async Task<string> Handle(CoachAskQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation("[CoachAsk] Received question. QuestionLength={Length}", request.Question.Length);

        var questionVector = await embeddingService.GenerateEmbeddingAsync(
            request.Question, EmbeddingTaskType.Query, cancellationToken);

        var similarReports = await embeddingRepository.SearchSimilarAsync(
            questionVector, currentUserService.UserId,
            ragOptions.Value.TopK, ragOptions.Value.SimilarityThreshold, cancellationToken);

        logger.LogInformation("[CoachAsk] Retrieved {Count} similar past sessions for context.", similarReports.Count);

        return await tennisCoachAdvisorOrchestrator.AskAsync(request.Question, similarReports, cancellationToken);
    }
}
