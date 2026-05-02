using AISportCoach.Application.Interfaces;
using AISportCoach.Application.Models;
using AISportCoach.Domain.Entities;
using MediatR;

namespace AISportCoach.Application.UseCases.GetReports;

public class GetReportsHandler(
    ICoachingReportRepository reportRepository,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetReportsQuery, PagedResult<CoachingReport>>
{
    public async Task<PagedResult<CoachingReport>> Handle(GetReportsQuery request, CancellationToken cancellationToken)
    {
        var pageSize = Math.Min(request.PageSize, 50);
        return await reportRepository.GetPagedByUserAsync(currentUserService.UserId, request.Page, pageSize, cancellationToken);
    }
}
