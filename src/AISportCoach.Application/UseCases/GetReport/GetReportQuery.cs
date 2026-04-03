using AISportCoach.Domain.Entities;
using MediatR;
namespace AISportCoach.Application.UseCases.GetReport;

public record GetReportQuery(Guid ReportId) : IRequest<CoachingReport>;
