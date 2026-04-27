using AISportCoach.Application.DTOs;
using AISportCoach.Application.Models;
using MediatR;

namespace AISportCoach.Application.UseCases.GetReportsSummary;

public record GetReportsSummaryQuery(int Page = 1, int PageSize = 10) : IRequest<PagedResult<CoachingReportSummary>>;
