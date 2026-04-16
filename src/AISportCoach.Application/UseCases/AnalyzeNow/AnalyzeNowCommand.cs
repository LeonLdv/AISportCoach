using AISportCoach.Domain.Entities;
using MediatR;
namespace AISportCoach.Application.UseCases.AnalyzeNow;

public record AnalyzeNowCommand(Guid VideoId, bool IncludeNtrpRating = true) : IRequest<CoachingReport>;
