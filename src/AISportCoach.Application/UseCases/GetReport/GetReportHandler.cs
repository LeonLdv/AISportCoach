using AISportCoach.Application.Interfaces;
using AISportCoach.Domain.Entities;
using AISportCoach.Domain.Exceptions;
using MediatR;

namespace AISportCoach.Application.UseCases.GetReport;

public class GetReportHandler(
    ICoachingReportRepository reportRepository,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetReportQuery, CoachingReport>
{
    public async Task<CoachingReport> Handle(GetReportQuery request, CancellationToken cancellationToken)
    {
        return await reportRepository.GetByIdAndUserAsync(request.ReportId, currentUserService.UserId, cancellationToken)
            ?? throw new ReportNotFoundException(request.ReportId);
    }
}
