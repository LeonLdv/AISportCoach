using AISportCoach.Application.Interfaces;
using AISportCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AISportCoach.Infrastructure.Persistence.Repositories;

public class ReportEmbeddingRepository(AppDbContext context) : IReportEmbeddingRepository
{
    public async Task AddAsync(ReportEmbedding embedding, CancellationToken ct)
    {
        // Use raw SQL because the Embedding column is vector(768), which requires an explicit
        // ::vector cast. EF Core's type system is bypassed for this column.
        var vectorLiteral = $"[{string.Join(",", embedding.Embedding)}]";
        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "ReportEmbeddings" ("Id", "CoachingReportId", "UserId", "Embedding", "CreatedAt")
            VALUES (@id, @reportId, @userId, @embedding::vector, @createdAt)
            """,
            [
                new NpgsqlParameter("id", embedding.Id),
                new NpgsqlParameter("reportId", embedding.CoachingReportId),
                new NpgsqlParameter("userId", embedding.UserId),
                new NpgsqlParameter("embedding", vectorLiteral),
                new NpgsqlParameter("createdAt", embedding.CreatedAt)
            ],
            ct);
    }

    public async Task<List<CoachingReport>> SearchSimilarAsync(
        float[] queryEmbedding, Guid userId, int topK, CancellationToken ct)
    {
        var vectorLiteral = $"[{string.Join(",", queryEmbedding)}]";

        var sql = $"""
            SELECT cr.* FROM "CoachingReports" cr
            JOIN "ReportEmbeddings" re ON re."CoachingReportId" = cr."Id"
            WHERE re."UserId" = @userId
            ORDER BY re."Embedding" <=> '{vectorLiteral}'::vector
            LIMIT @topK
            """;

        return await context.CoachingReports
            .FromSqlRaw(sql,
                new NpgsqlParameter("userId", userId),
                new NpgsqlParameter("topK", topK))
            .Include(r => r.Observations)
            .Include(r => r.Recommendations)
            .ToListAsync(ct);
    }
}
