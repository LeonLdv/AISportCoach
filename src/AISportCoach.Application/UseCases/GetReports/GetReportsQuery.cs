using AISportCoach.Application.Models;
using AISportCoach.Domain.Entities;
using MediatR;

namespace AISportCoach.Application.UseCases.GetReports;

public record GetReportsQuery(int Page = 1, int PageSize = 10) : IRequest<PagedResult<CoachingReport>>;
