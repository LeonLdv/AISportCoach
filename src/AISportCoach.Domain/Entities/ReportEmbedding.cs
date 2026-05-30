using AISportCoach.Domain.Enums;

namespace AISportCoach.Domain.Entities;

public class ReportEmbedding : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid CoachingReportId { get; private set; }
    public Guid UserId { get; private set; }
    public ChunkType ChunkType { get; private set; }
    public Guid ChunkId { get; private set; }
    public float[] Embedding { get; private set; } = [];
    public CoachingReport CoachingReport { get; private set; } = null!;

    private ReportEmbedding() { }

    public static ReportEmbedding Create(
        Guid reportId, Guid userId, ChunkType chunkType, Guid chunkId, float[] embedding) => new()
    {
        Id = Guid.CreateVersion7(),
        CoachingReportId = reportId,
        UserId = userId,
        ChunkType = chunkType,
        ChunkId = chunkId,
        Embedding = embedding,
    };
}
