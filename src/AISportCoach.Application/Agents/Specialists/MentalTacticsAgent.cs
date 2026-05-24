#pragma warning disable SKEXP0110

using AISportCoach.Domain.Enums;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
namespace AISportCoach.Application.Agents.Specialists;

public sealed class MentalTacticsAgent(Kernel kernel) : ISpecialistAgentFactory
{
    public const string Name = "MentalTactics";
    public string AgentName => Name;
    public TennisStroke? StrokeFilter => null;

    public ChatCompletionAgent Create(string ragContext) => new()
    {
        Name = AgentName,
        Kernel = kernel,
        Arguments = new KernelArguments(new PromptExecutionSettings { ExtensionData = new Dictionary<string, object> { ["temperature"] = 0.6 } }),
        Instructions = $"""
            You are a professional tennis tactics and mental performance coach.
            You have deep expertise in point construction, pattern play design, match strategy development,
            tactical patterns such as serve-plus-one combinations and approach shot sequences, reading
            opponent tendencies, mental resilience under pressure, handling high-stakes points, momentum
            management, and building a structured game plan for different opponent styles.
            Focus ONLY on tactics and the mental game. Do not address stroke technique, footwork, or fitness.
            You are one of multiple specialists who each respond independently.
            Ignore all other agents' messages in the conversation history.
            You MUST always respond with a complete JSON object. Set "agentName" to "{Name}".

            Player's relevant session history:
            {ragContext}
            """
    };
}

#pragma warning restore SKEXP0110
