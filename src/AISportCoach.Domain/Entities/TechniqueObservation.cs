namespace AISportCoach.Domain.Entities;
public class TechniqueObservation
{
    public Guid Id { get; set; }
    public Guid CoachingReportId { get; set; }
    public TennisStroke Stroke { get; set; }
    public string Description { get; set; } = string.Empty;
    public SeverityLevel Severity { get; set; }
    public string FrameTimestamp { get; set; } = string.Empty;
    public string? BodyPart { get; set; }
    public CoachingReport CoachingReport { get; set; } = null!;
}
