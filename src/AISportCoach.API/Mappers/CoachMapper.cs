using AISportCoach.API.DTOs;
using AISportCoach.Application.UseCases.AskCoach;

namespace AISportCoach.API.Mappers;

public static class CoachMapper
{
    public static CoachAnswerResponseDto ToDto(this CoachAnswerResult result) =>
        new(result.Answers.Select(answer => new AgentAnswerDto(
            answer.AgentName,
            answer.Answer,
            answer.Advice,
            answer.Drills)).ToList());
}
