using AISportCoach.Application.Models;
using AISportCoach.Domain.Entities;

namespace AISportCoach.Application.Interfaces;

public interface IReportChunker
{
    IReadOnlyList<ReportChunk> Chunk(CoachingReport report);
}
