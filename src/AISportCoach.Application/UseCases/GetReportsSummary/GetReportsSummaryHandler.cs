using AISportCoach.Application.DTOs;
using AISportCoach.Application.Interfaces;
using AISportCoach.Application.Models;
using MediatR;

namespace AISportCoach.Application.UseCases.GetReportsSummary;

public class GetReportsSummaryHandler(
    ICoachingReportRepository reportRepository,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetReportsSummaryQuery, PagedResult<CoachingReportSummary>>
{
    public async Task<PagedResult<CoachingReportSummary>> Handle(GetReportsSummaryQuery request, CancellationToken cancellationToken)
    {
        var pageSize = Math.Min(request.PageSize, 50);
        return await reportRepository.GetPagedSummariesByUserAsync(currentUserService.UserId, request.Page, pageSize, cancellationToken);
    }
}
