using System.ComponentModel.DataAnnotations;

namespace AISportCoach.API.DTOs;

public record CoachAskRequestDto([Required] string Question);

public record CoachAnswerDto(string Answer, string Advice, List<string> Drills);
