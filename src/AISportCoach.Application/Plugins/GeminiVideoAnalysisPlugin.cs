#pragma warning disable SKEXP0070
using AISportCoach.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.ComponentModel;
using System.Reflection;

namespace AISportCoach.Application.Plugins;

public class VideoAnalysisPlugin(ILogger<VideoAnalysisPlugin> logger)
{
    private static readonly string NtrpGuideContent = LoadNtrpGuide();

    private static string LoadNtrpGuide()
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "AISportCoach.Application.Plugins.Resources.ntrp_guide.md";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' not found. Verify EmbeddedResource in .csproj.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static readonly IReadOnlySet<string> AllStrokeNames =
        Enum.GetValues<AnalysisScope>()
            .Where(s => s != AnalysisScope.Ntrp)
            .Select(s => s.ToString())
            .ToHashSet();

    [KernelFunction("AnalyzeVideo")]
    [Description("Analyzes a tennis video. Returns JSON with observations and optional NTRP rating.")]
    public async Task<string> AnalyzeVideoAsync(
        Kernel kernel,
        [Description("File URI from the video upload service")] string fileUri,
        [Description("Analysis scopes requested")] IReadOnlySet<AnalysisScope> scopes)
    {
        logger.LogInformation(
            "[VideoAnalysis] Starting analysis. FileUri={FileUri}, Scopes={Scopes}",
            fileUri, string.Join(",", scopes));

        var requestedStrokes = scopes
            .Where(s => s != AnalysisScope.Ntrp)
            .Select(s => s.ToString())
            .ToHashSet();

        var includeNtrp = scopes.Contains(AnalysisScope.Ntrp);

        var strokeFilterLine = requestedStrokes.SetEquals(AllStrokeNames)
            ? "- stroke: one of Forehand, Backhand, Serve, Volley, Overhead, Footwork, General"
            : $"- stroke: report ONLY strokes from this list: {string.Join(", ", requestedStrokes)}. Ignore all others.";

        var ntrpBlock = BuildNtrpBlock(includeNtrp);
        var shapeNote = includeNtrp
            ? """Shape: { "observations": [...], "ntrp": {...} }"""
            : """Shape: { "observations": [...] }""";

        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        history.AddSystemMessage(
            "You are an expert tennis coach AI. Analyze tennis videos and identify specific technique issues.");

        var userMessage = new ChatMessageContent(AuthorRole.User, items:
        [
            new ImageContent(new Uri(fileUri)) { MimeType = "video/mp4" },
            new TextContent($$"""
                Analyze this tennis video and return a single JSON object.

                The JSON must have an "observations" array. Each observation:
                {{strokeFilterLine}}
                - description: specific technique issue
                - severity: Info | Warning | Critical
                - frameTimestamp: e.g. "0:05"
                - bodyPart: affected body part or null
                {{ntrpBlock}}

                Return ONLY valid JSON. No markdown fences. No other text.
                {{shapeNote}}
                """)
        ]);
        history.Add(userMessage);

        try
        {
            logger.LogInformation("[VideoAnalysis] Sending video to LLM. FileUri={FileUri}", fileUri);
            var response = await chatService.GetChatMessageContentAsync(history, kernel: kernel);
            var content = response.Content ?? """{ "observations": [] }""";
            logger.LogInformation("[VideoAnalysis] Response received. Length={Length}", content.Length);
            logger.LogDebug("[VideoAnalysis] Preview: {Preview}", content[..Math.Min(500, content.Length)]);
            return content;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[VideoAnalysis] LLM call failed. FileUri={FileUri}", fileUri);
            throw;
        }
    }

    private static string BuildNtrpBlock(bool includeNtrp)
    {
        if (!includeNtrp) return string.Empty;

        return $$"""


            Also produce an NTRP rating using the guide below.

            <NtrpGuide>
            {{NtrpGuideContent}}
            </NtrpGuide>

            Include a top-level "ntrp" key with this schema:
            {
              "ntrpRating": <one of: 1.5, 2.0, 2.5, 3.0, 3.5, 4.0, 4.5, 5.0, 5.5, 6.0, 6.5, 7.0>,
              "ntrpRatingRange": { "min": <lower>, "max": <upper> },
              "confidence": <"low" | "medium" | "high">,
              "ratingJustification": "<2-4 sentences>",
              "evidence": [
                {
                  "observation": "<what you saw>",
                  "ntrpIndicator": "<which NTRP characteristic>",
                  "supportedLevel": <numeric level>,
                  "weight": <"low" | "medium" | "high">
                }
              ]
            }
            """;
    }
}
