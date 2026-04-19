using AISportCoach.API.DTOs;
using AISportCoach.Domain.Entities;

namespace AISportCoach.API.Mappers;

public static class CoachingReportMapper
{
    public static CoachingReportResponseDto ToDto(this CoachingReport r) => new(
        r.Id,
        r.OverallScore,
        r.ExecutiveSummary,
        r.NtrpRating,
        r.NtrpRatingMin.HasValue && r.NtrpRatingMax.HasValue
            ? new NtrpRatingRangeDto(r.NtrpRatingMin.Value, r.NtrpRatingMax.Value)
            : null,
        r.NtrpConfidence,
        r.NtrpRatingJustification,
        r.NtrpEvidence.Select(e => new NtrpEvidenceDto(
            e.Observation, e.NtrpIndicator, e.SupportedLevel, e.Weight)).ToList(),
        r.Observations.Select(o => new TechniqueObservationDto(
            o.Stroke.ToString(), o.Description, o.Severity.ToString(), o.FrameTimestamp, o.BodyPart)).ToList(),
        r.Recommendations.Select(rec => new ImprovementRecommendationDto(
            rec.Title, rec.DetailedDescription, rec.Priority, rec.TargetStroke.ToString(), rec.DrillSuggestions)).ToList(),
        r.CreatedAt);
}
