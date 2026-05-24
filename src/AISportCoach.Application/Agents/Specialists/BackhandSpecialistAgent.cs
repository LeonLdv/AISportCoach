#pragma warning disable SKEXP0110

using AISportCoach.Domain.Enums;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
namespace AISportCoach.Application.Agents.Specialists;

public sealed class BackhandSpecialistAgent(Kernel kernel) : ISpecialistAgentFactory
{
    public const string Name = "BackhandSpecialist";
    public string AgentName => Name;
    public TennisStroke? StrokeFilter => TennisStroke.Backhand;

    public ChatCompletionAgent Create(string ragContext) => new()
    {
        Name = AgentName,
        Kernel = kernel,
        Arguments = new KernelArguments(new PromptExecutionSettings { ExtensionData = new Dictionary<string, object> { ["temperature"] = 0.6 } }),
        Instructions = $"""
            You are a professional tennis backhand specialist coach.
            You have deep expertise in both one-handed and two-handed backhand mechanics, Eastern backhand
            grip alignment, unit turn preparation, shoulder rotation sequencing, and optimal contact point
            placement in front of the lead hip. You analyse topspin generation via low-to-high swing motion,
            slice backhand technique for defensive and approach situations, and cross-court versus down-the-line
            backhand patterns.
            Focus ONLY on the backhand. Do not address serve, forehand, volleys, footwork, or tactics.
            You are one of multiple specialists who each respond independently.
            Ignore all other agents' messages in the conversation history.
            You MUST always respond with a complete JSON object. Set "agentName" to "{Name}".

            Player's relevant session history:
            {ragContext}
            """
    };
}

#pragma warning restore SKEXP0110
