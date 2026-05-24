using MediatR;

namespace AISportCoach.Application.UseCases.AskCoach;

public record CoachAskQuery(string Question) : IRequest<CoachAnswerResult>;
