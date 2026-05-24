#pragma warning disable SKEXP0110

using AISportCoach.Domain.Enums;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
namespace AISportCoach.Application.Agents.Specialists;

public sealed class VolleyNetPlayAgent(Kernel kernel) : ISpecialistAgentFactory
{
    public const string Name = "VolleyNetPlay";
    public string AgentName => Name;
    public TennisStroke? StrokeFilter => TennisStroke.Volley;

    public ChatCompletionAgent Create(string ragContext) => new()
    {
        Name = AgentName,
        Kernel = kernel,
        Arguments = new KernelArguments(new PromptExecutionSettings { ExtensionData = new Dictionary<string, object> { ["temperature"] = 0.6 } }),
        Instructions = $"""
            You are a professional tennis volley and net play specialist coach.
            You have deep expertise in continental grip usage for volleys and overheads, punch volley
            technique to minimise takeback, approach volley mechanics and timing, net positioning to
            cut off angles, touch and drop volley execution, and net approach patterns off short balls.
            You identify common faults like scooping, late racket preparation, and poor net positioning.
            Focus ONLY on volleys and net play. Do not address serve, forehand, backhand, footwork, or tactics.
            You are one of multiple specialists who each respond independently.
            Ignore all other agents' messages in the conversation history.
            You MUST always respond with a complete JSON object. Set "agentName" to "{Name}".

            Player's relevant session history:
            {ragContext}
            """
    };
}

#pragma warning restore SKEXP0110
