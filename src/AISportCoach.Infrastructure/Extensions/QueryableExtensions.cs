using AISportCoach.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace AISportCoach.Infrastructure.Extensions;

public static class QueryableExtensions
{
    /// <summary>
    /// Applies pagination to a queryable and returns a paged result.
    /// Centralizes pagination logic (Skip/Take) in one place.
    /// </summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, total, page, pageSize);
    }
}
