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

public class TennisQAOrchestrator(Kernel kernel, ILogger<TennisQAOrchestrator> logger)
{
    private static readonly TimeSpan ResultTimeout = TimeSpan.FromMinutes(2);

    public async Task<CoachAnswerResult> AskAsync(
        string question,
        List<CoachingReport> reports,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("[TennisQA] Processing question. Length={Length}", question.Length);

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
        var neededAgentNames = await TennisQAManager.ClassifyAsync(chatService, question, allAgentNames, cancellationToken);

        logger.LogInformation("[TennisQA] Classified to agents: {Agents}", string.Join(", ", neededAgentNames));

        var manager = new TennisQAManager(neededAgentNames);
        var orchestration = new GroupChatOrchestration(manager, serveAgent, backhandAgent, generalAgent);
        var augmentedQuestion = AugmentWithJsonInstruction(question);

        var runtime = new InProcessRuntime();
        await runtime.StartAsync();

        string rawJson;
        try
        {
            var result = await orchestration.InvokeAsync(augmentedQuestion, runtime);
            rawJson = await result.GetValueAsync(ResultTimeout);
            await runtime.RunUntilIdleAsync();
        }
        finally
        {
            await runtime.StopAsync();
        }

        logger.LogDebug("[TennisQA] Raw GroupChat response. Length={Length}", rawJson?.Length ?? 0);

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
            using var doc = JsonDocument.Parse(cleanJson, new JsonDocumentOptions { AllowTrailingCommas = true });
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Array)
                return ParseSingleObject(cleanJson, logger);

            var answers = new List<string>();
            var advices = new List<string>();
            var drills = new List<string>();

            foreach (var item in root.EnumerateArray())
            {
                if (item.TryGetProperty("answer", out var answerEl) && answerEl.GetString() is { } answer && answer.Length > 0)
                    answers.Add(answer);
                if (item.TryGetProperty("advice", out var adviceEl) && adviceEl.GetString() is { } advice && advice.Length > 0)
                    advices.Add(advice);
                if (item.TryGetProperty("drills", out var drillsEl))
                    foreach (var drill in drillsEl.EnumerateArray())
                        drills.Add(drill.GetString() ?? "");
            }

            return new CoachAnswerResult(
                string.Join("\n\n", answers),
                string.Join("\n\n", advices),
                drills);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "[TennisQA] Failed to parse merged response. Raw={Raw}",
                rawJson[..Math.Min(200, rawJson.Length)]);
            return Fallback();
        }
    }

    private static CoachAnswerResult ParseSingleObject(string rawJson, ILogger logger)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;
            var answer = root.TryGetProperty("answer", out var a) ? a.GetString() ?? "" : "";
            var advice = root.TryGetProperty("advice", out var adv) ? adv.GetString() ?? "" : "";
            var drills = new List<string>();
            if (root.TryGetProperty("drills", out var drillsEl))
                foreach (var drill in drillsEl.EnumerateArray())
                    drills.Add(drill.GetString() ?? "");
            return new CoachAnswerResult(answer, advice, drills);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "[TennisQA] Failed to parse single-object response. Raw={Raw}",
                rawJson[..Math.Min(200, rawJson.Length)]);
            return Fallback();
        }
    }

    private static CoachAnswerResult Fallback() =>
        new("I was unable to process your question at this time. Please try again.", "", []);

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

internal sealed class TennisQAManager : RoundRobinGroupChatManager
{
    private readonly IReadOnlyList<string> _neededAgentNames;
    private readonly Queue<string> _pendingAgents;

    public TennisQAManager(IReadOnlyList<string> neededAgentNames)
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
            .Where(n => allAgentNames.Any(a => string.Equals(a, n, StringComparison.OrdinalIgnoreCase)))
            .Select(n => allAgentNames.First(a => string.Equals(a, n, StringComparison.OrdinalIgnoreCase)))
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
            .Where(m => m.Role == AuthorRole.Assistant &&
                        _neededAgentNames.Any(n => string.Equals(n, m.AuthorName, StringComparison.OrdinalIgnoreCase)))
            .Select(m => ExtractJsonObject(m.Content ?? "{}"))
            .ToList();

        var merged = $"[{string.Join(",", responses)}]";
        return ValueTask.FromResult(new GroupChatManagerResult<string>(merged));
    }

    private static string ExtractJsonObject(string content)
    {
        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');
        return start >= 0 && end > start ? content[start..(end + 1)] : content;
    }
}

#pragma warning restore SKEXP0070, SKEXP0110
