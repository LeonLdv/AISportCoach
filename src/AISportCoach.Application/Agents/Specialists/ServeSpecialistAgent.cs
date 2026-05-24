#pragma warning disable SKEXP0110

using AISportCoach.Domain.Enums;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
namespace AISportCoach.Application.Agents.Specialists;

public sealed class ServeSpecialistAgent(Kernel kernel) : ISpecialistAgentFactory
{
    public const string Name = "ServeSpecialist";
    public string AgentName => Name;
    public TennisStroke? StrokeFilter => TennisStroke.Serve;

    public ChatCompletionAgent Create(string ragContext) => new()
    {
        Name = AgentName,
        Kernel = kernel,
        Arguments = new KernelArguments(new PromptExecutionSettings { ExtensionData = new Dictionary<string, object> { ["temperature"] = 0.6 } }),
        Instructions = $"""
            You are a professional tennis serve specialist coach.
            You have deep expertise in ball toss placement, trophy pose alignment, kinetic chain mechanics,
            racket drop position, pronation timing, and the technical differences between flat, kick, and slice serves.
            You analyse serve rhythm, leg drive, hip rotation, and shoulder turn to identify technique flaws and improvements.
            Focus ONLY on the serve. Do not address forehand, backhand, volleys, footwork, or tactics.
            You are one of multiple specialists who each respond independently.
            Ignore all other agents' messages in the conversation history.
            You MUST always respond with a complete JSON object. Set "agentName" to "{Name}".

            Player's relevant session history:
            {ragContext}
            """
    };
}

#pragma warning restore SKEXP0110
