# AI Pipeline — Semantic Kernel & Gemini

## Configuration

- Model ID → `Gemini:ModelId` config key (default `gemini-2.5-flash`) — never hardcode in code
- API key → `Gemini:ApiKey` config key — never hardcode
- Both read in `Infrastructure/DependencyInjection.cs` when building the `Kernel`

## Kernel & Plugin Registration

- One `Kernel` per DI scope: `services.AddScoped<Kernel>(...)`
- Plugins are `AddScoped<T>()` and injected into the orchestrator explicitly
- Plugins are **not** auto-discovered via `kernel.Plugins` — the orchestrator calls them directly
- See: `src/AISportCoach.Infrastructure/DependencyInjection.cs`

## Plugin Authoring

```csharp
// Each plugin method is decorated with [KernelFunction]
// Plugins receive Kernel as first parameter, then domain inputs
// Plugins return raw JSON strings — parsing is the orchestrator's responsibility
[KernelFunction("AnalyzeVideo")]
public async Task<string> AnalyzeVideoAsync(Kernel kernel, string fileUri, string playerLevel)
```

- System prompts live in the plugin class — they are code, not config
- Always request JSON output explicitly in the prompt
- Always strip LLM markdown fences before deserialising (use the `StripToJson` pattern already in the codebase)

## Orchestrator

- `TennisCoachOrchestrator` is the **sole entry point** for AI pipeline execution
- Handlers (`AnalyzeNowHandler`) call the orchestrator — never plugins directly
- Pipeline order: `VideoAnalysisPlugin` → `ReportGenerationPlugin` + `NtrpRatingPlugin`
- Parse responses with `JsonDocument` / `System.Text.Json` — no third-party JSON libs
- Log timing at each step with `Stopwatch`
- See: `src/AISportCoach.Application/Agents/TennisCoachOrchestrator.cs`

## Gemini File API

- Videos are uploaded via resumable upload; the URI is stored in `VideoUpload.GeminiFileUri`
- Poll for ACTIVE state before analysis — cap at 20 attempts × 5 s
- File URI caching avoids re-uploading on retry
- See: `src/AISportCoach.Infrastructure/VideoProcessing/GeminiFileService.cs`

## Resilience

- Cap polling retries; log a warning if max attempts reached before ACTIVE state
- Log `ElapsedMs` at each plugin call for performance monitoring
- Do not use `Task.Delay` polling loops in plugins — polling lives in `GeminiFileService`

## Patterns NOT Used Here

- No agent function-calling in the production pipeline — plugins are called directly
- No auto-discovered plugin registration via `kernel.ImportPluginFromObject`
- No OpenAI or Azure OpenAI connectors — Google AI Gemini only
