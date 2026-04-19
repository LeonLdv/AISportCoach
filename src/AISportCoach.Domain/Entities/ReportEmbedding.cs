namespace AISportCoach.Domain.Entities;

public class ReportEmbedding : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid CoachingReportId { get; private set; }
    public Guid UserId { get; private set; }
    public float[] Embedding { get; private set; } = [];
    public CoachingReport CoachingReport { get; private set; } = null!;

    private ReportEmbedding() { }

    public static ReportEmbedding Create(Guid reportId, Guid userId, float[] embedding) => new()
    {
        Id = Guid.CreateVersion7(),
        CoachingReportId = reportId,
        UserId = userId,
        Embedding = embedding,
    };
}
