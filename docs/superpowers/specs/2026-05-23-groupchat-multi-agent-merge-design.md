# GroupChat Multi-Agent Merge — Design Spec

**Date:** 2026-05-23  
**Scope:** `TennisQAManager` in `tests/AISportCoach.IntegrationTests/Integration/MagenticCoachQATest.cs`  
**Status:** Approved

## Problem

`TennisQAManager` routes every question to a single specialist (`MaximumInvocationCount = 1`). When a user asks about two stroke types in one question, the routing classifies one and ignores the other. The routing also had a secondary bug: it passed the full augmented message (including JSON formatting instructions) to the routing LLM, causing unpredictable classification and silent fallback to `_agentNames[0]`.

## Goal

Support questions that span multiple specialties by invoking all relevant specialists and returning their responses as a JSON array. Single-topic questions continue to invoke one specialist and return a single-element array.

## Approach: Pre-classification

One upfront LLM call classifies which agents are needed before the GroupChat starts. The result is passed into `TennisQAManager` so `MaximumInvocationCount` is set correctly at construction time, avoiding any reliance on dynamic property mutation during orchestration.

## Components

### `TennisQAManager.ClassifyAsync` (new static method)

```csharp
public static async Task<IReadOnlyList<string>> ClassifyAsync(
    IChatCompletionService chatService,
    string originalQuestion,        // NOT the augmented prompt
    IReadOnlyList<string> allAgentNames,
    CancellationToken ct = default)
```

- Sends a routing prompt asking for comma-separated agent name(s)
- Validates each returned name against `allAgentNames` (case-insensitive)
- Falls back to `allAgentNames` if parsing yields no valid names
- Uses `originalQuestion` only — the JSON formatting instruction is never included

### `TennisQAManager` constructor (changed)

```csharp
public TennisQAManager(IReadOnlyList<string> neededAgentNames)
```

- No longer takes `IChatCompletionService` — classification is done externally
- Populates `Queue<string> _pendingAgents` from `neededAgentNames`
- Sets `MaximumInvocationCount = neededAgentNames.Count`

### `SelectNextAgent` (simplified)

Dequeues the next name from `_pendingAgents`. No LLM call.

### `FilterResults` (changed)

Collects all `AuthorRole.Assistant` messages from `ChatHistory` where `AuthorName` is in `_neededAgentNames`. Wraps each raw JSON string into a JSON array: `"[{...},{...}]"`.

### `BuildOrchestration` (changed signature)

```csharp
private static GroupChatOrchestration BuildOrchestration(
    IReadOnlyList<string> neededAgentNames,
    ChatCompletionAgent serveAgent,
    ChatCompletionAgent backhandAgent)
```

`Kernel` parameter removed — the chat service is now obtained in the test body and passed to `ClassifyAsync`.

## Data Flow

```
Test body
  1. Build kernel, agents
  2. var chatService = kernel.GetRequiredService<IChatCompletionService>()
  3. ClassifyAsync(chatService, originalQuestion, allAgentNames)
       → LLM routing call → ["ServeAgent"] | ["OneHandBackhandAgent"] | ["ServeAgent","OneHandBackhandAgent"]
  4. TennisQAManager(neededAgentNames)
       → MaximumInvocationCount = neededAgentNames.Count
  5. GroupChatOrchestration(manager, serveAgent, backhandAgent)
  6. InvokeAsync(augmentedQuestion)

TennisQAManager during orchestration
  SelectNextAgent → dequeue next agent name
  Agent responds with JSON object → ChatHistory gains assistant message (AuthorName set)
  Repeat until queue empty
  FilterResults → collect assistant messages by AuthorName → "[{...},{...}]"

Test assertions
  7. Parse outer array
  8. Each element: answer (non-empty), advice (non-empty), drills (non-empty array), agentName (non-empty)
  9. Serve test: assert array length == 1
 10. Multi-topic test: assert array length == 2 with distinct agentNames
```

## Assertions

`AssertValidCoachResponse` updated to:
- Strip to `[...]` instead of `{...}`
- Assert `root.ValueKind == Array`
- Assert `GetArrayLength() > 0`
- For each element: validate `answer`, `advice`, `drills` (non-empty array), `agentName`

`drills` non-empty check added — this was previously missing, which allowed the wrong-agent refusal response to pass silently.

Per-test assertions added for expected array length and distinct `agentName` values.

## Out of Scope

`CoachConversationOrchestrator` (production) is not changed in this spec. A follow-on spec will apply a similar approach there.

## Files Changed

- `tests/AISportCoach.IntegrationTests/Integration/MagenticCoachQATest.cs`
