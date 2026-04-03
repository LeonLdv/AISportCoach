namespace AISportCoach.Domain.Entities;

public class NtrpEvidence
{
    public Guid Id { get; init; }
    public Guid CoachingReportId { get; init; }
    public string Observation { get; init; } = string.Empty;
    public string NtrpIndicator { get; init; } = string.Empty;
    public double SupportedLevel { get; init; }
    public string Weight { get; init; } = string.Empty;

    // Navigation
    public CoachingReport CoachingReport { get; init; } = null!;
}
