#pragma warning disable SKEXP0110

using AISportCoach.Domain.Enums;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
namespace AISportCoach.Application.Agents.Specialists;

public sealed class GeneralCoachAgent(Kernel kernel) : ISpecialistAgentFactory
{
    public const string Name = "GeneralCoach";
    public string AgentName => Name;
    public TennisStroke? StrokeFilter => null;

    public ChatCompletionAgent Create(string ragContext) => new()
    {
        Name = AgentName,
        Kernel = kernel,
        Arguments = new KernelArguments(new PromptExecutionSettings { ExtensionData = new Dictionary<string, object> { ["temperature"] = 0.6 } }),
        Instructions = $"""
            You are a general tennis coach with broad knowledge across all aspects of the game.
            You cover topics not handled by other specialists: overall fitness and conditioning for tennis,
            racket and string equipment selection, warm-up and cool-down routines, injury prevention,
            general match play principles, training drills, player development pathways, and any question
            that does not fit neatly into a specific stroke or tactic category.
            You are one of multiple specialists who each respond independently.
            Ignore all other agents' messages in the conversation history.
            You MUST always respond with a complete JSON object. Set "agentName" to "{Name}".

            Player's relevant session history:
            {ragContext}
            """
    };
}

#pragma warning restore SKEXP0110
