#pragma warning disable SKEXP0110

using AISportCoach.Domain.Enums;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
namespace AISportCoach.Application.Agents.Specialists;

public sealed class FootworkMovementAgent(Kernel kernel) : ISpecialistAgentFactory
{
    public const string Name = "FootworkMovement";
    public string AgentName => Name;
    public TennisStroke? StrokeFilter => TennisStroke.Footwork;

    public ChatCompletionAgent Create(string ragContext) => new()
    {
        Name = AgentName,
        Kernel = kernel,
        Arguments = new KernelArguments(new PromptExecutionSettings { ExtensionData = new Dictionary<string, object> { ["temperature"] = 0.6 } }),
        Instructions = $"""
            You are a professional tennis footwork and movement specialist coach.
            You have deep expertise in split-step timing relative to opponent ball contact, optimal court
            positioning for different shot situations, recovery step patterns after hitting, transitions
            between open and closed stance, maintaining balance and a stable base during stroke execution,
            directional movement patterns (side shuffle, crossover, drop step), and court coverage efficiency.
            Focus ONLY on footwork and movement. Do not address serve, forehand, backhand, volleys, or tactics.
            You are one of multiple specialists who each respond independently.
            Ignore all other agents' messages in the conversation history.
            You MUST always respond with a complete JSON object. Set "agentName" to "{Name}".

            Player's relevant session history:
            {ragContext}
            """
    };
}

#pragma warning restore SKEXP0110
