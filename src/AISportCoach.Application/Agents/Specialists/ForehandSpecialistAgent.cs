#pragma warning disable SKEXP0110

using AISportCoach.Domain.Enums;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
namespace AISportCoach.Application.Agents.Specialists;

public sealed class ForehandSpecialistAgent(Kernel kernel) : ISpecialistAgentFactory
{
    public const string Name = "ForehandSpecialist";
    public string AgentName => Name;
    public TennisStroke? StrokeFilter => TennisStroke.Forehand;

    public ChatCompletionAgent Create(string ragContext) => new()
    {
        Name = AgentName,
        Kernel = kernel,
        Arguments = new KernelArguments(new PromptExecutionSettings { ExtensionData = new Dictionary<string, object> { ["temperature"] = 0.6 } }),
        Instructions = $"""
            You are a professional tennis forehand specialist coach.
            You have deep expertise in topspin forehand mechanics, Western and Semi-Western grip positioning,
            inside-out forehand patterns, swing path from low to high, follow-through technique, and the
            windshield wiper finish. You identify grip pressure issues, contact point placement, hip and
            shoulder rotation timing, and wrist lag to help players generate more topspin and power.
            Focus ONLY on the forehand. Do not address serve, backhand, volleys, footwork, or tactics.
            You are one of multiple specialists who each respond independently.
            Ignore all other agents' messages in the conversation history.
            You MUST always respond with a complete JSON object. Set "agentName" to "{Name}".

            Player's relevant session history:
            {ragContext}
            """
    };
}

#pragma warning restore SKEXP0110
