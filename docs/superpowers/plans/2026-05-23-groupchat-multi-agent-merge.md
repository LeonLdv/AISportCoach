# GroupChat Multi-Agent Merge — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Update `TennisQAManager` to pre-classify which specialists a question needs, invoke only those agents, and return their responses as a JSON array instead of a single object.

**Architecture:** A new static `ClassifyAsync` method makes one LLM routing call with the original (unaugmented) question before `GroupChatOrchestration` starts. The result is passed into `TennisQAManager`'s constructor so `MaximumInvocationCount` is set at construction time. `FilterResults` collects all assistant messages by `AuthorName` and wraps them in a JSON array.

**Tech Stack:** .NET 10, xUnit 2.x, Semantic Kernel 1.x (`GroupChatOrchestration`, `RoundRobinGroupChatManager`), Google Gemini via `IChatCompletionService`

---

## File Map

| File | Change |
|------|--------|
| `tests/AISportCoach.IntegrationTests/Integration/MagenticCoachQATest.cs` | All changes — `TennisQAManager`, `BuildOrchestration`, test methods, assertions |

---

### Task 1: Add `ClassifyAsync` static method to `TennisQAManager`

This is a pure addition — nothing else changes, code compiles and existing tests still pass after this step.

**Files:**
- Modify: `tests/AISportCoach.IntegrationTests/Integration/MagenticCoachQATest.cs:227`

- [ ] **Step 1: Add `ClassifyAsync` inside `TennisQAManager`, before `SelectNextAgent`**

Replace lines 203–225 (the existing `SelectNextAgent`) with the block below, inserting `ClassifyAsync` above it:

```csharp
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
```

- [ ] **Step 2: Build to confirm no compilation errors**

```bash
dotnet build tests/AISportCoach.IntegrationTests
```

Expected: build succeeds with no errors.

- [ ] **Step 3: Commit**

```bash
git add tests/AISportCoach.IntegrationTests/Integration/MagenticCoachQATest.cs
git commit -m "Add TennisQAManager.ClassifyAsync static routing method"
```

---

### Task 2: Refactor `TennisQAManager` + update `BuildOrchestration` + update test method bodies

All three must change in one step because changing the constructor signature immediately breaks `BuildOrchestration`, and changing `BuildOrchestration`'s signature immediately breaks both test methods.

**Files:**
- Modify: `tests/AISportCoach.IntegrationTests/Integration/MagenticCoachQATest.cs`

- [ ] **Step 1: Replace the entire `TennisQAManager` class (lines 191–234)**

```csharp
// ── TennisQAManager ─────────────────────────────────────────────────────────

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

    public override ValueTask<GroupChatManagerResult<string>> SelectNextAgent(
        ChatHistory history,
        GroupChatTeam team,
        CancellationToken cancellationToken = default)
    {
        var nextAgent = _pendingAgents.TryDequeue(out var name) ? name : _neededAgentNames[0];
        return ValueTask.FromResult(new GroupChatManagerResult<string>(nextAgent) { Reason = $"Routing to {nextAgent}" });
    }

    public override ValueTask<GroupChatManagerResult<string>> FilterResults(
        ChatHistory history,
        CancellationToken cancellationToken = default)
    {
        var responses = history
            .Where(m => m.Role == AuthorRole.Assistant &&
                        _neededAgentNames.Any(n => string.Equals(n, m.AuthorName, StringComparison.OrdinalIgnoreCase)))
            .Select(m => m.Content ?? "{}")
            .ToList();

        var merged = $"[{string.Join(",", responses)}]";
        return ValueTask.FromResult(new GroupChatManagerResult<string>(merged));
    }
}
```

- [ ] **Step 2: Replace `BuildOrchestration` (lines 136–144)**

```csharp
    private static GroupChatOrchestration BuildOrchestration(
        IReadOnlyList<string> neededAgentNames,
        ChatCompletionAgent serveAgent,
        ChatCompletionAgent backhandAgent)
    {
        var manager = new TennisQAManager(neededAgentNames);
        return new GroupChatOrchestration(manager, serveAgent, backhandAgent);
    }
```

- [ ] **Step 3: Replace `AskAboutServe_ReturnsStructuredJson` body (lines 26–47)**

```csharp
    [Fact]
    public async Task AskAboutServe_ReturnsStructuredJson()
    {
        var question = "My ball toss keeps drifting to the right and I'm losing power on my serve. What should I fix?";
        var augmented = AugmentWithJsonInstruction(question);

        var kernel = BuildKernel(ReadApiKey());
        var serveAgent = BuildServeAgent(kernel);
        var backhandAgent = BuildOneHandBackhandAgent(kernel);

        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        IReadOnlyList<string> allAgentNames = [serveAgent.Name!, backhandAgent.Name!];
        var neededAgents = await TennisQAManager.ClassifyAsync(chatService, question, allAgentNames);
        var orchestration = BuildOrchestration(neededAgents, serveAgent, backhandAgent);

        var runtime = new InProcessRuntime();
        await runtime.StartAsync();

        var result = await orchestration.InvokeAsync(augmented, runtime);
        var rawJson = await result.GetValueAsync(TimeSpan.FromMinutes(2));

        await runtime.RunUntilIdleAsync();
        await runtime.StopAsync();

        AssertValidCoachResponse(rawJson, expectedCount: 1);
    }
```

- [ ] **Step 4: Replace `AskAboutOneHandBackhand_ReturnsStructuredJson` body (lines 49–71)**

```csharp
    [Fact]
    public async Task AskAboutOneHandBackhand_ReturnsStructuredJson()
    {
        var question = "My one-hand backhand keeps going into the net and my Serve unstable. What am I doing wrong and how do I fix it?";
        var augmented = AugmentWithJsonInstruction(question);

        var kernel = BuildKernel(ReadApiKey());
        var serveAgent = BuildServeAgent(kernel);
        var backhandAgent = BuildOneHandBackhandAgent(kernel);

        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        IReadOnlyList<string> allAgentNames = [serveAgent.Name!, backhandAgent.Name!];
        var neededAgents = await TennisQAManager.ClassifyAsync(chatService, question, allAgentNames);
        var orchestration = BuildOrchestration(neededAgents, serveAgent, backhandAgent);

        var runtime = new InProcessRuntime();
        await runtime.StartAsync();

        var result = await orchestration.InvokeAsync(augmented, runtime);
        var rawJson = await result.GetValueAsync(TimeSpan.FromMinutes(2));

        await runtime.RunUntilIdleAsync();
        await runtime.StopAsync();

        _testOutputHelper.WriteLine(rawJson);
        AssertValidCoachResponse(rawJson, expectedCount: 2);

        // Both specialists must respond with distinct agent names
        var arrayStart = rawJson!.IndexOf('[');
        using var doc = JsonDocument.Parse(rawJson[arrayStart..], new JsonDocumentOptions { AllowTrailingCommas = true });
        var distinctAgentNames = doc.RootElement.EnumerateArray()
            .Select(el => el.GetProperty("agentName").GetString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        Assert.Equal(2, distinctAgentNames.Count);
    }
```

- [ ] **Step 5: Build to confirm no compilation errors**

```bash
dotnet build tests/AISportCoach.IntegrationTests
```

Expected: build succeeds with no errors.

- [ ] **Step 6: Commit**

```bash
git add tests/AISportCoach.IntegrationTests/Integration/MagenticCoachQATest.cs
git commit -m "Refactor TennisQAManager to pre-classify and merge multi-agent responses"
```

---

### Task 3: Update `AssertValidCoachResponse` for JSON array

**Files:**
- Modify: `tests/AISportCoach.IntegrationTests/Integration/MagenticCoachQATest.cs:160`

- [ ] **Step 1: Replace `AssertValidCoachResponse` (lines 160–186)**

```csharp
    private static void AssertValidCoachResponse(string? rawJson, int expectedCount = 1)
    {
        Assert.NotNull(rawJson);
        Assert.NotEmpty(rawJson);

        var jsonStart = rawJson.IndexOf('[');
        var jsonEnd = rawJson.LastIndexOf(']');
        var cleanJson = jsonStart >= 0 && jsonEnd > jsonStart
            ? rawJson[jsonStart..(jsonEnd + 1)]
            : rawJson;

        using var doc = JsonDocument.Parse(cleanJson, new JsonDocumentOptions { AllowTrailingCommas = true });
        var root = doc.RootElement;

        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Equal(expectedCount, root.GetArrayLength());

        foreach (var item in root.EnumerateArray())
        {
            Assert.True(item.TryGetProperty("answer", out var answerEl),   "missing 'answer'");
            Assert.NotEmpty(answerEl.GetString() ?? "");

            Assert.True(item.TryGetProperty("advice", out var adviceEl),   "missing 'advice'");
            Assert.NotEmpty(adviceEl.GetString() ?? "");

            Assert.True(item.TryGetProperty("drills", out var drillsEl),   "missing 'drills'");
            Assert.Equal(JsonValueKind.Array, drillsEl.ValueKind);
            Assert.True(drillsEl.GetArrayLength() > 0, "drills must not be empty");

            Assert.True(item.TryGetProperty("agentName", out var agentEl), "missing 'agentName'");
            Assert.NotEmpty(agentEl.GetString() ?? "");
        }
    }
```

- [ ] **Step 2: Build to confirm no compilation errors**

```bash
dotnet build tests/AISportCoach.IntegrationTests
```

Expected: build succeeds with no errors.

- [ ] **Step 3: Commit**

```bash
git add tests/AISportCoach.IntegrationTests/Integration/MagenticCoachQATest.cs
git commit -m "Update AssertValidCoachResponse to validate JSON array with non-empty drills"
```

---

### Task 4: Run tests and verify

- [ ] **Step 1: Run both tests**

```bash
dotnet test tests/AISportCoach.IntegrationTests --filter "FullyQualifiedName~MagenticCoachQATest" --logger "console;verbosity=detailed"
```

Expected output:
```
Passed  AskAboutServe_ReturnsStructuredJson
Passed  AskAboutOneHandBackhand_ReturnsStructuredJson
```

The `AskAboutOneHandBackhand` test output (via `ITestOutputHelper`) should show a JSON array with two objects — one from `ServeAgent` (or equivalent name) and one from `OneHandBackhandAgent`.

- [ ] **Step 2: If `AskAboutServe` fails with `expectedCount: 1`**

The classification LLM might have decided the serve question also needs the backhand agent (unlikely but possible). Add a log line to the test to inspect what was classified:

```csharp
_testOutputHelper.WriteLine($"Classified agents: {string.Join(", ", neededAgents)}");
```

Re-run to see the classification output, then tighten the routing prompt in `ClassifyAsync` to make single-topic questions route to one agent only.

- [ ] **Step 3: If `AskAboutOneHandBackhand` fails with `expectedCount: 2`**

The classification LLM might have returned only one agent for a clearly multi-topic question. Inspect `_testOutputHelper` output to see the raw `rawJson`. If classification returned 1 agent:
- The routing prompt in `ClassifyAsync` may need stronger instruction. Update the prompt:

```csharp
        var prompt = $"""
            A user asks a tennis coaching question. Identify ALL specialists needed to fully answer it.
            Available specialists: {agentList}
            - ServeAgent: answers questions about serve mechanics, ball toss, trophy pose, pronation
            - OneHandBackhandAgent: answers questions about one-handed backhand grip, contact point, swing path
            Question: {originalQuestion}
            Reply with ONLY the specialist name(s) needed, comma-separated. Include ALL relevant specialists.
            Example single: ServeAgent
            Example multiple: ServeAgent, OneHandBackhandAgent
            """;
```

- [ ] **Step 4: Commit if any prompt adjustments were made**

```bash
git add tests/AISportCoach.IntegrationTests/Integration/MagenticCoachQATest.cs
git commit -m "Tune ClassifyAsync routing prompt for multi-topic detection"
```
