using AISportCoach.Application.Models;
using AISportCoach.Domain.Entities;

namespace AISportCoach.Application.Interfaces;

public interface IReportEmbeddingRepository
{
    Task AddChunksAsync(
        Guid userId,
        IReadOnlyList<(ReportChunk Chunk, float[] Embedding)> chunks,
        CancellationToken ct);

    Task<List<CoachingReport>> SearchSimilarAsync(
        float[] queryEmbedding, Guid userId, int topK, double maxDistance, CancellationToken ct);
}
