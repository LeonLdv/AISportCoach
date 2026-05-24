#pragma warning disable SKEXP0110

using AISportCoach.Application.Agents.Helpers;
using AISportCoach.Application.UseCases.AskCoach;
using AISportCoach.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
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

        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var allAgentNames = agentFactories.Select(f => f.AgentName).ToList();

        var selectedAgentNames = await CoachGroupChatManager.ClassifyAsync(
            chatService, question, allAgentNames, cancellationToken);

        logger.LogInformation("[TennisCoachAdvisor] Classified to agents: {Agents}",
            string.Join(", ", selectedAgentNames));

        var answers = new List<CoachAgentAnswer>();

        foreach (var factory in agentFactories.Where(f =>
            selectedAgentNames.Any(name => string.Equals(name, f.AgentName, StringComparison.OrdinalIgnoreCase))))
        {
            logger.LogInformation("[TennisCoachAdvisor] Invoking {Agent}.", factory.AgentName);

            var agent = factory.Create(RagContextFormatter.Format(reports, factory.StrokeFilter));

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(agent.Instructions ?? "");
            chatHistory.AddUserMessage(AugmentWithJsonInstruction(question));

            var response = await chatService.GetChatMessageContentAsync(
                chatHistory,
                new PromptExecutionSettings { ExtensionData = new Dictionary<string, object> { ["temperature"] = 0.6 } },
                kernel,
                cancellationToken);

            logger.LogInformation("[TennisCoachAdvisor] {Agent} responded. Length={Length}",
                factory.AgentName, response.Content?.Length ?? 0);

            var answer = ParseSingleAnswer(factory.AgentName, response.Content ?? "");
            if (answer is not null) answers.Add(answer);
        }

        return new CoachAnswerResult(answers);
    }

    private static string AugmentWithJsonInstruction(string question) =>
        $$"""
        {{question}}

        Respond as ONE specialist. Provide your own complete, independent JSON object
        focused solely on your area of expertise. Ignore other agents' responses.
        Required format — all four fields mandatory:
        {"answer":"<1-2 sentence direct answer>","advice":"<2-4 sentence coaching advice>","drills":["...","...","..."],"agentName":"<your name>"}
        No markdown fences. No prose outside the JSON.
        """;

    private CoachAgentAnswer? ParseSingleAnswer(string agentName, string content)
    {
        try
        {
            var openBraceIndex = content.IndexOf('{');
            var closeBraceIndex = content.LastIndexOf('}');

            if (openBraceIndex < 0 || closeBraceIndex <= openBraceIndex)
            {
                logger.LogWarning("[TennisCoachAdvisor] {Agent} returned no JSON object.", agentName);
                return null;
            }

            var jsonObject = content[openBraceIndex..(closeBraceIndex + 1)];

            using var jsonDocument = JsonDocument.Parse(jsonObject,
                new JsonDocumentOptions { AllowTrailingCommas = true });

            var element = jsonDocument.RootElement;

            return new CoachAgentAnswer(
                AgentName: element.TryGetProperty("agentName", out var nameEl) ? nameEl.GetString() ?? agentName : agentName,
                Answer:    element.TryGetProperty("answer",    out var answerEl) ? answerEl.GetString() ?? "" : "",
                Advice:    element.TryGetProperty("advice",    out var adviceEl) ? adviceEl.GetString() ?? "" : "",
                Drills:    element.TryGetProperty("drills",    out var drillsEl)
                    ? drillsEl.EnumerateArray().Select(d => d.GetString() ?? "").ToList()
                    : []);
        }
        catch (JsonException jsonException)
        {
            logger.LogWarning(jsonException, "[TennisCoachAdvisor] {Agent} returned invalid JSON.", agentName);
            return null;
        }
    }
}

#pragma warning restore SKEXP0110
