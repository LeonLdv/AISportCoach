using AISportCoach.Application.DTOs;
using AISportCoach.Application.Models;
using AISportCoach.Domain.Entities;

namespace AISportCoach.Application.Interfaces;

public interface ICoachingReportRepository
{
    Task<CoachingReport?> GetByIdAsync(Guid reportId, CancellationToken ct = default);
    Task<CoachingReport?> GetByIdAndUserAsync(Guid reportId, Guid userId, CancellationToken ct = default);
    Task<PagedResult<CoachingReport>> GetPagedAsync(int page, int pageSize, CancellationToken ct = default);
    Task<PagedResult<CoachingReport>> GetPagedByUserAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);
    Task<PagedResult<CoachingReportSummary>> GetPagedSummariesAsync(int page, int pageSize, CancellationToken ct = default);
    Task<PagedResult<CoachingReportSummary>> GetPagedSummariesByUserAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(CoachingReport report, CancellationToken ct = default);
}
