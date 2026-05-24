#pragma warning disable SKEXP0110

using AISportCoach.Domain.Enums;
using Microsoft.SemanticKernel.Agents;

namespace AISportCoach.Application.Agents;

public interface ISpecialistAgentFactory
{
    string AgentName { get; }
    TennisStroke? StrokeFilter { get; }
    ChatCompletionAgent Create(string ragContext);
}

#pragma warning restore SKEXP0110
