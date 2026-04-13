namespace AISportCoach.Domain.Entities;

public class CoachingReport
{
    public Guid Id { get; private set; }
    public Guid VideoUploadId { get; private set; }
    public int OverallScore { get; private set; }
    public string ExecutiveSummary { get; private set; } = string.Empty;
    public DateTime GeneratedAt { get; private set; }

    // NTRP rating
    public double? NtrpRating { get; private set; }
    public double? NtrpRatingMin { get; private set; }
    public double? NtrpRatingMax { get; private set; }
    public string? NtrpConfidence { get; private set; }
    public string? NtrpRatingJustification { get; private set; }

    // Navigation
    public VideoUpload VideoUpload { get; private set; } = null!;
    public List<TechniqueObservation> Observations { get; private set; } = [];
    public List<ImprovementRecommendation> Recommendations { get; private set; } = [];
    public List<NtrpEvidence> NtrpEvidence { get; private set; } = [];

    private CoachingReport() { }

    public static CoachingReport Create(
        Guid videoUploadId,
        int overallScore,
        string executiveSummary,
        List<TechniqueObservation> observations,
        List<ImprovementRecommendation> recommendations,
        double? ntrpRating = null,
        double? ntrpRatingMin = null,
        double? ntrpRatingMax = null,
        string? ntrpConfidence = null,
        string? ntrpRatingJustification = null,
        List<NtrpEvidence>? ntrpEvidence = null)
    {
        return new CoachingReport
        {
            Id = Guid.CreateVersion7(),
            VideoUploadId = videoUploadId,
            OverallScore = overallScore,
            ExecutiveSummary = executiveSummary,
            Observations = observations,
            Recommendations = recommendations,
            NtrpRating = ntrpRating,
            NtrpRatingMin = ntrpRatingMin,
            NtrpRatingMax = ntrpRatingMax,
            NtrpConfidence = ntrpConfidence,
            NtrpRatingJustification = ntrpRatingJustification,
            NtrpEvidence = ntrpEvidence ?? [],
            GeneratedAt = DateTime.UtcNow
        };
    }
}
