using AISportCoach.Domain.Entities;
using MediatR;
namespace AISportCoach.Application.UseCases.AnalyzeNow;

public record AnalyzeNowCommand(Guid VideoId) : IRequest<CoachingReport>;
