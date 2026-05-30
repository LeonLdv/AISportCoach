using AISportCoach.Application.Models;
using AISportCoach.Domain.Entities;

namespace AISportCoach.Application.Interfaces;

/// <summary>
/// Chunks a coaching report into semantic units for embedding and storage.
/// </summary>
public interface IReportChunker
{
    /// <summary>
    /// Chunks a coaching report into discrete text fragments.
    /// </summary>
    /// <param name="report">The coaching report to chunk.</param>
    /// <returns>A read-only list of report chunks.</returns>
    IReadOnlyList<ReportChunk> Chunk(CoachingReport report);
}
