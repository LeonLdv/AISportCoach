using System.ComponentModel.DataAnnotations;

namespace AISportCoach.API.DTOs;

public record CoachAskRequestDto([Required] string Question);

public record AgentAnswerDto(
    string AgentName,
    string Answer,
    string Advice,
    IReadOnlyList<string> Drills);

public record CoachAnswerResponseDto(IReadOnlyList<AgentAnswerDto> Answers);
