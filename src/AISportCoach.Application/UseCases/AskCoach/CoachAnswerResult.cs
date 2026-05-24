namespace AISportCoach.Application.UseCases.AskCoach;

public record CoachAgentAnswer(
    string AgentName,
    string Answer,
    string Advice,
    IReadOnlyList<string> Drills);

public record CoachAnswerResult(IReadOnlyList<CoachAgentAnswer> Answers);
