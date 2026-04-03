using AISportCoach.Domain.Entities;
namespace AISportCoach.Application.Interfaces;
public interface ICoachingReportRepository
{
    Task<CoachingReport?> GetByIdAsync(Guid reportId, CancellationToken ct = default);
    Task<(List<CoachingReport> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(CoachingReport report, CancellationToken ct = default);
}
