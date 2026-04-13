using MediatR;

namespace AISportCoach.Application.UseCases.AskCoach;

public record CoachAskQuery(string Question) : IRequest<CoachAnswerResult>;

public record CoachAnswerResult(string Answer, string Advice, List<string> Drills);
