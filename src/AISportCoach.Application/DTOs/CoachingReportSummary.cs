namespace AISportCoach.Application.DTOs;

public record CoachingReportSummary(
    Guid Id,
    int OverallScore,
    string ExecutiveSummary,
    double? NtrpRating,
    DateTime CreatedAt);
