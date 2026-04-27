using AISportCoach.Application.DTOs;
using AISportCoach.Application.Interfaces;
using AISportCoach.Application.Models;
using AISportCoach.Domain.Entities;
using AISportCoach.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;

namespace AISportCoach.Infrastructure.Persistence.Repositories;

public class CoachingReportRepository(AppDbContext db) : ICoachingReportRepository
{
    public Task<CoachingReport?> GetByIdAsync(Guid reportId, CancellationToken ct = default)
        => db.CoachingReports
             .AsNoTracking()
             .Include(r => r.Observations)
             .Include(r => r.Recommendations)
             .Include(r => r.NtrpEvidence)
             .FirstOrDefaultAsync(r => r.Id == reportId, ct);

    public async Task<PagedResult<CoachingReport>> GetPagedAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        return await db.CoachingReports
            .AsNoTracking()
            .Include(r => r.Observations)
            .Include(r => r.Recommendations)
            .Include(r => r.NtrpEvidence)
            .OrderByDescending(r => r.CreatedAt)
            .ToPagedResultAsync(page, pageSize, ct);
    }

    public async Task<PagedResult<CoachingReportSummary>> GetPagedSummariesAsync(int page, int pageSize, CancellationToken ct = default)
    {
        return await db.CoachingReports
            .AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new CoachingReportSummary(
                r.Id,
                r.OverallScore,
                r.ExecutiveSummary,
                r.NtrpRating,
                r.CreatedAt
            ))
            .ToPagedResultAsync(page, pageSize, ct);
    }

    public async Task AddAsync(CoachingReport report, CancellationToken ct = default)
    {
        db.CoachingReports.Add(report);
        await db.SaveChangesAsync(ct);
    }
}
