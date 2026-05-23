#pragma warning disable SKEXP0070, SKEXP0110

using AISportCoach.Application.Agents.Helpers;
using AISportCoach.Application.UseCases.AskCoach;
using AISportCoach.Domain.Entities;
using AISportCoach.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Orchestration.GroupChat;
using Microsoft.SemanticKernel.Agents.Runtime.InProcess;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text.Json;

namespace AISportCoach.Application.Agents;

public class TennisCoachAdvisorOrchestrator(Kernel kernel, ILogger<TennisCoachAdvisorOrchestrator> logger)
{
    private static readonly TimeSpan ResultTimeout = TimeSpan.FromMinutes(2);

    public async Task<CoachAnswerResult> AskAsync(
        string question,
        List<CoachingReport> reports,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("[TennisCoachAdvisor] Processing question. Length={Length}", question.Length);

        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        var serveAgent = BuildAgent(
            "ServeAgent",
            "Expert in tennis serve technique: ball toss, trophy pose, pronation, flat/kick/slice serve mechanics, and serve improvement drills",
            ServeAgentPrompt(RagContextFormatter.Format(reports, TennisStroke.Serve)));

        var backhandAgent = BuildAgent(
            "OneHandBackhandAgent",
            "Expert in one-handed backhand technique: grip, unit turn, contact point, follow-through, topspin generation, and backhand improvement drills",
            BackhandAgentPrompt(RagContextFormatter.Format(reports, TennisStroke.Backhand)));

        var generalAgent = BuildAgent(
            "GeneralAgent",
            "Expert tennis coach covering all aspects: strategy, footwork, mental game, fitness, and match play",
            GeneralAgentPrompt(RagContextFormatter.Format(reports, null)));

        IReadOnlyList<string> allAgentNames = [serveAgent.Name!, backhandAgent.Name!, generalAgent.Name!];
        var neededAgentNames = await TennisCoachAdvisorManager.ClassifyAsync(chatService, question, allAgentNames, cancellationToken);

        logger.LogInformation("[TennisCoachAdvisor] Classified to agents: {Agents}", string.Join(", ", neededAgentNames));

        var manager = new TennisCoachAdvisorManager(neededAgentNames);
        var orchestration = new GroupChatOrchestration(manager, serveAgent, backhandAgent, generalAgent);
        var augmentedQuestion = AugmentWithJsonInstruction(question);

        var runtime = new InProcessRuntime();
        await runtime.StartAsync(cancellationToken);

        string rawJson;
        try
        {
            var result = await orchestration.InvokeAsync(augmentedQuestion, runtime, cancellationToken);
            rawJson = await result.GetValueAsync(ResultTimeout, cancellationToken);
            await runtime.RunUntilIdleAsync();
        }
        finally
        {
            await runtime.StopAsync(cancellationToken);
        }

        logger.LogDebug("[TennisCoachAdvisor] Raw GroupChat response. Length={Length}", rawJson?.Length ?? 0);

        return Merge(rawJson, logger);
    }

    private ChatCompletionAgent BuildAgent(string name, string description, string instructions) =>
        new() { Name = name, Description = description, Instructions = instructions, Kernel = kernel };

    private static CoachAnswerResult Merge(string? rawJson, ILogger logger)
    {
        if (string.IsNullOrEmpty(rawJson))
            return Fallback();

        var jsonStart = rawJson.IndexOf('[');
        var jsonEnd = rawJson.LastIndexOf(']');
        var cleanJson = jsonStart >= 0 && jsonEnd > jsonStart
            ? rawJson[jsonStart..(jsonEnd + 1)]
            : rawJson;

        try
        {
            using var jsonDocument = JsonDocument.Parse(cleanJson, new JsonDocumentOptions { AllowTrailingCommas = true });
            var root = jsonDocument.RootElement;

            if (root.ValueKind != JsonValueKind.Array)
                return new CoachAnswerResult([ParseAgentAnswer(root, "Coach")]);

            var agentAnswers = new List<AgentAnswer>();
            foreach (var item in root.EnumerateArray())
                agentAnswers.Add(ParseAgentAnswer(item, "Coach"));

            return agentAnswers.Count > 0 ? new CoachAnswerResult(agentAnswers) : Fallback();
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "[TennisCoachAdvisor] Failed to parse merged response. Raw={Raw}",
                rawJson[..Math.Min(200, rawJson.Length)]);
            return Fallback();
        }
    }

    private static AgentAnswer ParseAgentAnswer(JsonElement item, string defaultAgentName)
    {
        var agentName = item.TryGetProperty("agentName", out var nameEl) ? nameEl.GetString() ?? defaultAgentName : defaultAgentName;
        var answer = item.TryGetProperty("answer", out var answerEl) ? answerEl.GetString() ?? "" : "";
        var advice = item.TryGetProperty("advice", out var adviceEl) ? adviceEl.GetString() ?? "" : "";
        var drills = new List<string>();
        if (item.TryGetProperty("drills", out var drillsEl))
            foreach (var drill in drillsEl.EnumerateArray())
                drills.Add(drill.GetString() ?? "");
        return new AgentAnswer(agentName, answer, advice, drills);
    }

    private static CoachAnswerResult Fallback() =>
        new([new AgentAnswer("Coach", "I was unable to process your question at this time. Please try again.", "", [])]);

    private static string AugmentWithJsonInstruction(string question) => $$"""
        {{question}}

        Respond with a single valid JSON object and nothing else:
        {
          "answer": "<direct 1-2 sentence answer to the part of the question in your area of expertise>",
          "advice": "<detailed coaching advice, 2-4 sentences, strictly within your specialty>",
          "drills": ["<drill 1>", "<drill 2>", "<drill 3>"]
        }
        No markdown fences. No prose outside the JSON object.
        """;

    private static string ServeAgentPrompt(string ragContext) => $"""
        You are a professional tennis coach specialising exclusively in serve technique.
        Your expertise covers: ball toss placement, trophy pose, kinetic chain, racket drop,
        pronation, flat/kick/slice serve mechanics, and serve rhythm.
        You may be invoked alongside other specialists. Focus ONLY on the serve aspects of
        the question. Ignore other topics and do not echo or repeat what other specialists said.
        If the question has no serve component, briefly decline and return empty drills.

        Player's past session observations (serve-related):
        {ragContext}
        """;

    private static string BackhandAgentPrompt(string ragContext) => $"""
        You are a professional tennis coach specialising exclusively in the one-handed backhand.
        Your expertise covers: Eastern backhand grip, unit turn, shoulder rotation, contact point
        (in front of lead hip), swing path, topspin generation via low-to-high motion, and full
        follow-through over the shoulder.
        You may be invoked alongside other specialists. Focus ONLY on the one-handed backhand
        aspects of the question. Ignore other topics and do not echo or repeat what other
        specialists said. If the question has no backhand component, briefly decline and return empty drills.

        Player's past session observations (backhand-related):
        {ragContext}
        """;

    private static string GeneralAgentPrompt(string ragContext) => $"""
        You are a professional tennis coach with expertise in all aspects of the game including
        strategy, footwork, mental game, fitness, and overall match play.
        Focus on the player's overall progress and general coaching needs.

        Player's past session history:
        {ragContext}
        """;
}

internal sealed class TennisCoachAdvisorManager : RoundRobinGroupChatManager
{
    private readonly IReadOnlyList<string> _neededAgentNames;
    private readonly Queue<string> _pendingAgents;

    public TennisCoachAdvisorManager(IReadOnlyList<string> neededAgentNames)
    {
        _neededAgentNames = neededAgentNames;
        _pendingAgents = new Queue<string>(neededAgentNames);
        MaximumInvocationCount = neededAgentNames.Count;
    }

    public static async Task<IReadOnlyList<string>> ClassifyAsync(
        IChatCompletionService chatService,
        string question,
        IReadOnlyList<string> allAgentNames,
        CancellationToken cancellationToken = default)
    {
        var agentList = string.Join(", ", allAgentNames);
        var prompt = $"""
            Route this tennis coaching question to the correct specialist(s).
            Available specialists: {agentList}
            Question: {question}
            Reply with ONLY the specialist name(s) that should answer, comma-separated if multiple.
            Example single: ServeAgent
            Example multiple: ServeAgent, OneHandBackhandAgent
            """;

        var response = await chatService.GetChatMessageContentAsync(prompt, cancellationToken: cancellationToken);
        var content = response.Content?.Trim() ?? string.Empty;

        var selected = content
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(candidate => allAgentNames.Any(knownName => string.Equals(knownName, candidate, StringComparison.OrdinalIgnoreCase)))
            .Select(candidate => allAgentNames.First(knownName => string.Equals(knownName, candidate, StringComparison.OrdinalIgnoreCase)))
            .Distinct()
            .ToList();

        return selected.Count > 0 ? selected : allAgentNames;
    }

    public override ValueTask<GroupChatManagerResult<string>> SelectNextAgent(
        ChatHistory history,
        GroupChatTeam team,
        CancellationToken cancellationToken = default)
    {
        if (!_pendingAgents.TryDequeue(out var name))
            throw new InvalidOperationException("SelectNextAgent called more times than MaximumInvocationCount.");
        return ValueTask.FromResult(new GroupChatManagerResult<string>(name) { Reason = $"Routing to {name}" });
    }

    public override ValueTask<GroupChatManagerResult<string>> FilterResults(
        ChatHistory history,
        CancellationToken cancellationToken = default)
    {
        var responses = history
            .Where(message => message.Role == AuthorRole.Assistant &&
                              _neededAgentNames.Any(knownAgentName => string.Equals(knownAgentName, message.AuthorName, StringComparison.OrdinalIgnoreCase)))
            .Select(message => InjectAgentName(ExtractJsonObject(message.Content ?? "{}"), message.AuthorName ?? "Coach"))
            .ToList();

        var merged = $"[{string.Join(",", responses)}]";
        return ValueTask.FromResult(new GroupChatManagerResult<string>(merged));
    }

    private static string ExtractJsonObject(string content)
    {
        var openBraceIndex = content.IndexOf('{');
        var closeBraceIndex = content.LastIndexOf('}');
        return openBraceIndex >= 0 && closeBraceIndex > openBraceIndex ? content[openBraceIndex..(closeBraceIndex + 1)] : content;
    }

    private static string InjectAgentName(string jsonObject, string agentName)
    {
        var closeBraceIndex = jsonObject.LastIndexOf('}');
        if (closeBraceIndex < 0) return jsonObject;
        var escaped = agentName.Replace("\"", "\\\"");
        return $"{jsonObject[..closeBraceIndex]},\"agentName\":\"{escaped}\"}}";
    }
}

#pragma warning restore SKEXP0070, SKEXP0110
