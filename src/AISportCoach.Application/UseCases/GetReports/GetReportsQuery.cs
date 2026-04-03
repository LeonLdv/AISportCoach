using AISportCoach.Domain.Entities;
using MediatR;
namespace AISportCoach.Application.UseCases.GetReports;

public record GetReportsQuery(int Page = 1, int PageSize = 10) : IRequest<PagedReportsResult>;
public record PagedReportsResult(List<CoachingReport> Items, int TotalCount, int Page, int PageSize);
