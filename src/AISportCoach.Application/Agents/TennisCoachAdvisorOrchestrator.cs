#pragma warning disable SKEXP0110

using AISportCoach.Application.Agents.Helpers;
using AISportCoach.Application.UseCases.AskCoach;
using AISportCoach.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Orchestration.GroupChat;
using Microsoft.SemanticKernel.Agents.Runtime.InProcess;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text.Json;

namespace AISportCoach.Application.Agents;

public class TennisCoachAdvisorOrchestrator(
    IEnumerable<ISpecialistAgentFactory> agentFactories,
    Kernel kernel,
    ILogger<TennisCoachAdvisorOrchestrator> logger)
{
    public async Task<CoachAnswerResult> AskAsync(
        string question,
        List<CoachingReport> reports,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("[TennisCoachAdvisor] Processing question. Length={Length}", question.Length);

        var agents = agentFactories
            .Select(factory => factory.Create(RagContextFormatter.Format(reports, factory.StrokeFilter)))
            .ToList();

        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var agentNames = agents.Select(agent => agent.Name!).ToList();

        var selectedAgentNames = await CoachGroupChatManager.ClassifyAsync(
            chatService, question, agentNames, cancellationToken);

        logger.LogInformation("[TennisCoachAdvisor] Classified to agents: {Agents}",
            string.Join(", ", selectedAgentNames));

        var manager = new CoachGroupChatManager(selectedAgentNames, logger);
        var orchestration = new GroupChatOrchestration(manager, [.. agents]);

        var runtime = new InProcessRuntime();
        await runtime.StartAsync();

        try
        {
            var invocationResult = await orchestration.InvokeAsync(
                AugmentWithJsonInstruction(question), runtime);

            var rawJson = await invocationResult.GetValueAsync(TimeSpan.FromMinutes(2));

            await runtime.RunUntilIdleAsync();

            logger.LogDebug("[TennisCoachAdvisor] Raw response. Length={Length}", rawJson?.Length ?? 0);

            return ParseResult(rawJson);
        }
        finally
        {
            await runtime.StopAsync();
        }
    }

    private static string AugmentWithJsonInstruction(string question) =>
        $$"""
        {{question}}

        You are responding as ONE specialist in a panel. Provide YOUR OWN COMPLETE and INDEPENDENT
        JSON object focused solely on your area of expertise. Do not be affected by other agents'
        responses already in this conversation:
        {
          "answer":    "<direct 1-2 sentence answer to the part of the question in your area>",
          "advice":    "<detailed coaching advice, 2-4 sentences, strictly within your specialty>",
          "drills":    ["<drill 1>", "<drill 2>", "<drill 3>"],
          "agentName": "<your specialist name>"
        }
        No markdown fences. No prose outside the JSON object. All four fields are required.
        """;

    private CoachAnswerResult ParseResult(string? rawJson)
    {
        if (string.IsNullOrEmpty(rawJson))
            return new CoachAnswerResult([]);

        try
        {
            var openBracketIndex = rawJson.IndexOf('[');
            var closeBracketIndex = rawJson.LastIndexOf(']');

            if (openBracketIndex < 0 || closeBracketIndex <= openBracketIndex)
                return new CoachAnswerResult([]);

            var cleanJson = rawJson[openBracketIndex..(closeBracketIndex + 1)];

            using var jsonDocument = JsonDocument.Parse(cleanJson,
                new JsonDocumentOptions { AllowTrailingCommas = true });

            var answers = jsonDocument.RootElement.EnumerateArray()
                .Select(element => new CoachAgentAnswer(
                    AgentName: element.GetProperty("agentName").GetString() ?? "",
                    Answer:    element.GetProperty("answer").GetString() ?? "",
                    Advice:    element.GetProperty("advice").GetString() ?? "",
                    Drills:    element.GetProperty("drills").EnumerateArray()
                                   .Select(drill => drill.GetString() ?? "")
                                   .ToList()))
                .ToList();

            return new CoachAnswerResult(answers);
        }
        catch (JsonException jsonException)
        {
            logger.LogWarning(jsonException,
                "[TennisCoachAdvisor] Failed to parse agent response JSON.");
            return new CoachAnswerResult([]);
        }
    }
}

#pragma warning restore SKEXP0110
