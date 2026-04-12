using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace AISportCoach.API.DTOs;

public record VideoResponseDto(
    Guid Id,
    string OriginalFileName,
    long FileSizeBytes,
    string Status,
    DateTime UploadedAt);

public record TechniqueObservationDto(
    string Stroke,
    string Description,
    string Severity,
    string FrameTimestamp,
    string? BodyPart);

public record ImprovementRecommendationDto(
    string Title,
    string DetailedDescription,
    int Priority,
    string TargetStroke,
    List<string> DrillSuggestions);

public record NtrpRatingRangeDto(double Min, double Max);

public record NtrpEvidenceDto(
    string Observation,
    string NtrpIndicator,
    double SupportedLevel,
    string Weight);

public record CoachingReportResponseDto(
    Guid Id,
    int OverallScore,
    string ExecutiveSummary,
    double? NtrpRating,
    NtrpRatingRangeDto? NtrpRatingRange,
    string? NtrpConfidence,
    string? NtrpRatingJustification,
    List<NtrpEvidenceDto> NtrpEvidence,
    List<TechniqueObservationDto> Observations,
    List<ImprovementRecommendationDto> Recommendations,
    DateTime GeneratedAt);

public record PagedReportsResponseDto(
    List<CoachingReportResponseDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public record PaginationQuery(
    [property: FromQuery(Name = "page")]
    [property: Range(1, int.MaxValue, ErrorMessage = "Page must be at least 1.")]
    int Page = 1,

    [property: FromQuery(Name = "pageSize")]
    [property: Range(1, 100, ErrorMessage = "Page size must be between 1 and 100.")]
    int PageSize = 10);
