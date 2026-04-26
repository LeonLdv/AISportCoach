using AISportCoach.Application.Interfaces;
using AISportCoach.Domain.Entities;
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

    public async Task<(List<CoachingReport> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        var total = await db.CoachingReports.CountAsync(ct);

        var items = await db.CoachingReports
            .AsNoTracking()
            .Include(r => r.Observations)
            .Include(r => r.Recommendations)
            .Include(r => r.NtrpEvidence)
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task AddAsync(CoachingReport report, CancellationToken ct = default)
    {
        db.CoachingReports.Add(report);
        await db.SaveChangesAsync(ct);
    }
}
