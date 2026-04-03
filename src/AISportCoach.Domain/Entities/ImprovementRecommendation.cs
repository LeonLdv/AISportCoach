namespace AISportCoach.Domain.Entities;
public class ImprovementRecommendation
{
    public Guid Id { get; set; }
    public Guid CoachingReportId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string DetailedDescription { get; set; } = string.Empty;
    public int Priority { get; set; }
    public TennisStroke TargetStroke { get; set; }
    public List<string> DrillSuggestions { get; set; } = [];
    public CoachingReport CoachingReport { get; set; } = null!;
}
