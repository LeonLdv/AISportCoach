using AISportCoach.Domain.Entities;
using AISportCoach.Domain.Enums;
using MediatR;

namespace AISportCoach.Application.UseCases.AnalyzeNow;

public record AnalyzeNowCommand(Guid VideoId, IReadOnlySet<AnalysisScope> Scopes) : IRequest<CoachingReport>
{
    public static readonly IReadOnlySet<AnalysisScope> AllScopes =
        new HashSet<AnalysisScope>((AnalysisScope[])Enum.GetValues(typeof(AnalysisScope)));

    public static AnalyzeNowCommand ForAllScopes(Guid videoId) => new(videoId, AllScopes);
}
