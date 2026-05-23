#pragma warning disable SKEXP0070
#pragma warning disable SKEXP0110

using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Orchestration.GroupChat;
using Microsoft.SemanticKernel.Agents.Runtime.InProcess;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using Xunit.Abstractions;

namespace AISportCoach.IntegrationTests.Integration;

public class MagenticCoachQATest
{
    private readonly ITestOutputHelper _testOutputHelper;

    public MagenticCoachQATest(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    private const string Model = "gemini-2.5-pro";

    [Fact]
    public async Task AskAboutServe_ReturnsStructuredJson()
    {
        var question = "My ball toss keeps drifting to the right and I'm losing power on my serve. What should I fix?";
        var augmented = AugmentWithJsonInstruction(question);

        var kernel = BuildKernel(ReadApiKey());
        var serveAgent = BuildServeAgent(kernel);
        var backhandAgent = BuildOneHandBackhandAgent(kernel);
        var orchestration = BuildOrchestration(kernel, serveAgent, backhandAgent);

        var runtime = new InProcessRuntime();
        await runtime.StartAsync();

        var result = await orchestration.InvokeAsync(augmented, runtime);
        var rawJson = await result.GetValueAsync(TimeSpan.FromMinutes(2));

        await runtime.RunUntilIdleAsync();
        await runtime.StopAsync();

        AssertValidCoachResponse(rawJson);
    }

    [Fact]
    public async Task AskAboutOneHandBackhand_ReturnsStructuredJson()
    {
        var question = "My one-hand backhand keeps going into the net and my Serve unstable. What am I doing wrong and how do I fix it?";
        var augmented = AugmentWithJsonInstruction(question);

        var kernel = BuildKernel(ReadApiKey());
        var serveAgent = BuildServeAgent(kernel);
        var backhandAgent = BuildOneHandBackhandAgent(kernel);
        var orchestration = BuildOrchestration(kernel, serveAgent, backhandAgent);

        var runtime = new InProcessRuntime();
        await runtime.StartAsync();

        var result = await orchestration.InvokeAsync(augmented, runtime);
        var rawJson = await result.GetValueAsync(TimeSpan.FromMinutes(2));

        await runtime.RunUntilIdleAsync();
        await runtime.StopAsync();

        _testOutputHelper.WriteLine(rawJson);
        AssertValidCoachResponse(rawJson);
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private static string ReadApiKey()
    {
        var fromEnv = Environment.GetEnvironmentVariable("Gemini__ApiKey");
        if (!string.IsNullOrWhiteSpace(fromEnv)) return fromEnv;

        foreach (var fileName in new[] { "secrets.json", "appsettings.test.json" })
        {
            var path = Path.Combine(AppContext.BaseDirectory, fileName);
            if (!File.Exists(path)) continue;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("Gemini", out var gemini) &&
                gemini.TryGetProperty("ApiKey", out var key))
            {
                var value = key.GetString();
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
        }

        throw new InvalidOperationException("Gemini:ApiKey not set.");
    }

    private static Kernel BuildKernel(string apiKey) =>
        Kernel.CreateBuilder()
            .AddGoogleAIGeminiChatCompletion(Model, apiKey)
            .Build();

    private static ChatCompletionAgent BuildServeAgent(Kernel kernel) => new()
    {
        Name = "ServeAgent",
        Description = "Expert in tennis serve technique: ball toss, trophy pose, pronation, flat/kick/slice serve mechanics, and serve improvement drills",
        Instructions =
            """
            You are a professional tennis coach specialising exclusively in serve technique.
            Your expertise covers: ball toss placement, trophy pose, kinetic chain, racket drop,
            pronation, flat/kick/slice serve mechanics, and serve rhythm.
            Provide practical, specific coaching advice for the serve only.
            If asked to respond in JSON format, do so exactly as requested.
            If the question is not about the serve, say so briefly and decline to answer.
            """,
        Kernel = kernel,
        Arguments = new KernelArguments(new GeminiPromptExecutionSettings { Temperature = 0.6 })
    };

    private static ChatCompletionAgent BuildOneHandBackhandAgent(Kernel kernel) => new()
    {
        Name = "OneHandBackhandAgent",
        Description = "Expert in one-handed backhand technique: grip, unit turn, contact point, follow-through, topspin generation, and backhand improvement drills",
        Instructions =
            """
            You are a professional tennis coach specialising exclusively in the one-handed backhand.
            Your expertise covers: Eastern backhand grip, unit turn, shoulder rotation, contact point
            (in front of lead hip), swing path, topspin generation via low-to-high motion, and full
            follow-through over the shoulder.
            Provide practical, specific coaching advice for the one-handed backhand only.
            If asked to respond in JSON format, do so exactly as requested.
            If the question is not about the one-handed backhand, say so briefly and decline to answer.
            """,
        Kernel = kernel,
        Arguments = new KernelArguments(new GeminiPromptExecutionSettings { Temperature = 0.6 })
    };

    private static GroupChatOrchestration BuildOrchestration(
        Kernel kernel,
        ChatCompletionAgent serveAgent,
        ChatCompletionAgent backhandAgent)
    {
        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var manager = new TennisQAManager(chatService, [serveAgent, backhandAgent]);
        return new GroupChatOrchestration(manager, serveAgent, backhandAgent);
    }

    private static string AugmentWithJsonInstruction(string question) =>
        $$"""
        {{question}}

        After consulting the relevant specialist, respond with a single valid JSON object and nothing else:
        {
          "answer":    "<direct 1-2 sentence answer>",
          "advice":    "<detailed coaching advice, 2-4 sentences>",
          "drills":    ["<drill 1>", "<drill 2>", "<drill 3>"],
          "agentName": "<your agent name>"
        }
        No markdown fences. No prose outside the JSON object.
        """;

    private static void AssertValidCoachResponse(string? rawJson)
    {
        Assert.NotNull(rawJson);
        Assert.NotEmpty(rawJson);

        // Strip markdown fences the LLM may add despite instructions
        var jsonStart = rawJson.IndexOf('{');
        var jsonEnd = rawJson.LastIndexOf('}');
        var cleanJson = jsonStart >= 0 && jsonEnd > jsonStart
            ? rawJson[jsonStart..(jsonEnd + 1)]
            : rawJson;

        using var doc = JsonDocument.Parse(cleanJson, new JsonDocumentOptions { AllowTrailingCommas = true });
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("answer", out var answerEl),   "missing 'answer'");
        Assert.NotEmpty(answerEl.GetString() ?? "");

        Assert.True(root.TryGetProperty("advice", out var adviceEl),   "missing 'advice'");
        Assert.NotEmpty(adviceEl.GetString() ?? "");

        Assert.True(root.TryGetProperty("drills", out var drillsEl),   "missing 'drills'");
        Assert.Equal(JsonValueKind.Array, drillsEl.ValueKind);

        Assert.True(root.TryGetProperty("agentName", out var agentEl), "missing 'agentName'");
        Assert.NotEmpty(agentEl.GetString() ?? "");
    }
}

// ── TennisQAManager ─────────────────────────────────────────────────────────

internal sealed class TennisQAManager : RoundRobinGroupChatManager
{
    private readonly IChatCompletionService _chatService;
    private readonly IReadOnlyList<string> _agentNames;

    public TennisQAManager(IChatCompletionService chatService, IEnumerable<Agent> agents)
    {
        _chatService = chatService;
        _agentNames = agents.Select(a => a.Name!).ToList();
        MaximumInvocationCount = 1;
    }

    public static async Task<IReadOnlyList<string>> ClassifyAsync(
        IChatCompletionService chatService,
        string originalQuestion,
        IReadOnlyList<string> allAgentNames,
        CancellationToken cancellationToken = default)
    {
        var agentList = string.Join(", ", allAgentNames);
        var prompt = $"""
            Route this tennis coaching question to the correct specialist(s).
            Available specialists: {agentList}
            Question: {originalQuestion}
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

    public override async ValueTask<GroupChatManagerResult<string>> SelectNextAgent(
        ChatHistory history,
        GroupChatTeam team,
        CancellationToken cancellationToken)
    {
        var question = history.FirstOrDefault(m => m.Role == AuthorRole.User)?.Content ?? string.Empty;
        var agentList = string.Join(", ", _agentNames);

        var routingPrompt = $"""
            Route this tennis coaching question to the correct specialist.
            Available specialists: {agentList}
            Question: {question}
            Reply with ONLY the specialist name. Nothing else.
            """;

        var response = await _chatService.GetChatMessageContentAsync(routingPrompt, cancellationToken: cancellationToken);
        var selectedName = response.Content?.Trim() ?? string.Empty;

        var validName = _agentNames.FirstOrDefault(n =>
            string.Equals(n, selectedName, StringComparison.OrdinalIgnoreCase)) ?? _agentNames[0];

        return new GroupChatManagerResult<string>(validName) { Reason = $"Routing to {validName}" };
    }

    public override ValueTask<GroupChatManagerResult<string>> FilterResults(
        ChatHistory history,
        CancellationToken cancellationToken)
    {
        var lastResponse = history.LastOrDefault(m => m.Role == AuthorRole.Assistant)?.Content ?? string.Empty;
        return ValueTask.FromResult(new GroupChatManagerResult<string>(lastResponse));
    }
}

#pragma warning restore SKEXP0110
#pragma warning restore SKEXP0070
