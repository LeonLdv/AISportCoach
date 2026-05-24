#pragma warning disable SKEXP0110

using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Agents.Orchestration.GroupChat;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AISportCoach.Application.Agents;

internal sealed class CoachGroupChatManager : RoundRobinGroupChatManager
{
    private readonly IReadOnlyList<string> _selectedAgentNames;
    private readonly Queue<string> _pendingAgents;
    private readonly ILogger _logger;

    public CoachGroupChatManager(IReadOnlyList<string> selectedAgentNames, ILogger logger)
    {
        _selectedAgentNames = selectedAgentNames;
        _pendingAgents = new Queue<string>(selectedAgentNames);
        _logger = logger;
        MaximumInvocationCount = selectedAgentNames.Count;
    }

    public static async Task<IReadOnlyList<string>> ClassifyAsync(
        IChatCompletionService chatService,
        string question,
        IReadOnlyList<string> allAgentNames,
        CancellationToken cancellationToken = default)
    {
        var agentDescriptions = string.Join("\n", AgentDescriptions
            .Where(pair => allAgentNames.Contains(pair.Key))
            .Select(pair => $"- {pair.Key}: {pair.Value}"));

        var prompt = $"""
            Route this tennis coaching question to the correct specialist(s).

            Available specialists:
            {agentDescriptions}

            Question: {question}

            Reply with ONLY the specialist name(s) comma-separated. Include only specialists
            whose area is directly relevant. If unclear, include GeneralCoach.
            """;

        var response = await chatService.GetChatMessageContentAsync(prompt, cancellationToken: cancellationToken);
        var content = response.Content?.Trim() ?? string.Empty;

        var selected = content
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(candidate => allAgentNames.Any(name =>
                string.Equals(name, candidate, StringComparison.OrdinalIgnoreCase)))
            .Select(candidate => allAgentNames.First(name =>
                string.Equals(name, candidate, StringComparison.OrdinalIgnoreCase)))
            .Distinct()
            .ToList();

        return selected.Count > 0 ? selected : (IReadOnlyList<string>)["GeneralCoach"];
    }

    public override ValueTask<GroupChatManagerResult<string>> SelectNextAgent(
        ChatHistory history,
        GroupChatTeam team,
        CancellationToken cancellationToken = default)
    {
        if (!_pendingAgents.TryDequeue(out var agentName))
            throw new InvalidOperationException(
                "SelectNextAgent called more times than MaximumInvocationCount.");

        _logger.LogDebug("[CoachGroupChatManager] Routing to {Agent}.", agentName);
        return ValueTask.FromResult(new GroupChatManagerResult<string>(agentName) { Reason = $"Routing to {agentName}" });
    }

    public override ValueTask<GroupChatManagerResult<string>> FilterResults(
        ChatHistory history,
        CancellationToken cancellationToken = default)
    {
        var jsonParts = history
            .Where(m => m.Role == AuthorRole.Assistant &&
                        _selectedAgentNames.Any(name =>
                            string.Equals(name, m.AuthorName, StringComparison.OrdinalIgnoreCase)))
            .Select(m => ExtractJsonObject(m.Content ?? "{}"))
            .ToList();

        var merged = $"[{string.Join(",", jsonParts)}]";

        _logger.LogDebug("[CoachGroupChatManager] Merged {Count} agent responses.", jsonParts.Count);

        return ValueTask.FromResult(new GroupChatManagerResult<string>(merged));
    }

    private static string ExtractJsonObject(string content)
    {
        var openBraceIndex = content.IndexOf('{');
        var closeBraceIndex = content.LastIndexOf('}');
        return openBraceIndex >= 0 && closeBraceIndex > openBraceIndex
            ? content[openBraceIndex..(closeBraceIndex + 1)]
            : content;
    }

    private static readonly Dictionary<string, string> AgentDescriptions = new()
    {
        ["ServeSpecialist"]    = "ball toss, pronation, flat/kick/slice serve mechanics",
        ["ForehandSpecialist"] = "topspin forehand, grip, inside-out, swing path",
        ["BackhandSpecialist"] = "one/two-handed backhand, slice, unit turn",
        ["VolleyNetPlay"]      = "volleys, overheads, net approach, touch volleys",
        ["FootworkMovement"]   = "split-step, court positioning, recovery steps",
        ["MentalTactics"]      = "point construction, pattern play, match strategy",
        ["GeneralCoach"]       = "fitness, equipment, warm-up, general tennis questions"
    };
}

#pragma warning restore SKEXP0110
