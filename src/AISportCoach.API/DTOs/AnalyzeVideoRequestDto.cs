using AISportCoach.Domain.Enums;

namespace AISportCoach.API.DTOs;

public record AnalyzeVideoRequestDto(AnalysisScope[]? Scopes = null);
