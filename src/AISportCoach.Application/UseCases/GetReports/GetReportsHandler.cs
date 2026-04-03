using AISportCoach.Application.Interfaces;
using MediatR;

namespace AISportCoach.Application.UseCases.GetReports;

public class GetReportsHandler(ICoachingReportRepository reportRepository)
    : IRequestHandler<GetReportsQuery, PagedReportsResult>
{
    public async Task<PagedReportsResult> Handle(GetReportsQuery request, CancellationToken cancellationToken)
    {
        var pageSize = Math.Min(request.PageSize, 50);
        var (items, totalCount) = await reportRepository.GetPagedAsync(request.Page, pageSize, cancellationToken);
        return new PagedReportsResult(items, totalCount, request.Page, pageSize);
    }
}
