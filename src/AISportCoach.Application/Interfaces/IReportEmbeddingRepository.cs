using AISportCoach.Domain.Entities;

namespace AISportCoach.Application.Interfaces;

public interface IReportEmbeddingRepository
{
    Task AddAsync(ReportEmbedding embedding, CancellationToken ct);
    Task<List<CoachingReport>> SearchSimilarAsync(
        float[] queryEmbedding, Guid userId, int topK, CancellationToken ct);
}
