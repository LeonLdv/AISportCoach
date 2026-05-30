using System.Globalization;
using AISportCoach.Application.Interfaces;
using AISportCoach.Application.Models;
using AISportCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AISportCoach.Infrastructure.Persistence.Repositories;

public class ReportEmbeddingRepository(AppDbContext context) : IReportEmbeddingRepository
{
    private static string ToVectorLiteral(float[] values) =>
        $"[{string.Join(",", values.Select(f => f.ToString(CultureInfo.InvariantCulture)))}]";

    public async Task AddChunksAsync(
        Guid userId,
        IReadOnlyList<(ReportChunk Chunk, float[] Embedding)> chunks,
        CancellationToken ct)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        var createdAt = DateTime.UtcNow;

        foreach (var (chunk, embedding) in chunks)
        {
            var vectorLiteral = ToVectorLiteral(embedding);
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO "ReportEmbeddings" ("Id", "CoachingReportId", "UserId", "ChunkType", "ChunkId", "Embedding", "CreatedAt")
                VALUES (@id, @reportId, @userId, @chunkType, @chunkId, @embedding::vector, @createdAt)
                """,
                [
                    new NpgsqlParameter("id", Guid.CreateVersion7()),
                    new NpgsqlParameter("reportId", chunk.ReportId),
                    new NpgsqlParameter("userId", userId),
                    new NpgsqlParameter("chunkType", chunk.ChunkType.ToString()),
                    new NpgsqlParameter("chunkId", chunk.ChunkId),
                    new NpgsqlParameter("embedding", vectorLiteral),
                    new NpgsqlParameter("createdAt", createdAt)
                ],
                ct);
        }

        await transaction.CommitAsync(ct);
    }

    public async Task<List<CoachingReport>> SearchSimilarAsync(
        float[] queryEmbedding, Guid userId, int topK, double maxDistance, CancellationToken ct)
    {
        var vectorLiteral = ToVectorLiteral(queryEmbedding);

        // CTE finds the TopK reports with the closest-matching chunk (deduplicates across chunks).
        var sql = $"""
            WITH ranked AS (
                SELECT "CoachingReportId",
                       MIN("Embedding" <=> '{vectorLiteral}'::vector) AS min_distance
                FROM "ReportEmbeddings"
                WHERE "UserId" = @userId
                  AND "Embedding" <=> '{vectorLiteral}'::vector < @maxDistance
                GROUP BY "CoachingReportId"
                ORDER BY min_distance
                LIMIT @topK
            )
            SELECT cr.* FROM "CoachingReports" cr
            JOIN ranked ON ranked."CoachingReportId" = cr."Id"
            ORDER BY ranked.min_distance
            """;

        return await context.CoachingReports
            .FromSqlRaw(sql,
                new NpgsqlParameter("userId", userId),
                new NpgsqlParameter<double>("maxDistance", maxDistance),
                new NpgsqlParameter("topK", topK))
            .AsNoTracking()
            .Include(r => r.Observations)
            .ToListAsync(ct);
    }
}
