#pragma warning disable SKEXP0070
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.ComponentModel;
using System.Diagnostics;

namespace AISportCoach.Application.Plugins;

public class VideoAnalysisPlugin(ILogger<VideoAnalysisPlugin> logger)
{
    [KernelFunction("AnalyzeVideo")]
    [Description("Analyzes a tennis video via its file URI. Returns a JSON array of technique observations.")]
    public async Task<string> AnalyzeVideoAsync(
        Kernel kernel,
        [Description("File URI returned by the video upload service")] string fileUri)
    {
        logger.LogInformation(
            "[VideoAnalysis] Starting video analysis. FileUri={FileUri}",
            fileUri);

        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();

        history.AddSystemMessage(
            "You are an expert tennis coach AI. Analyze tennis videos and identify specific technique issues.");

        var userMessage = new ChatMessageContent(AuthorRole.User, items:
        [
            new ImageContent(new Uri(fileUri)) { MimeType = "video/mp4" },
            new TextContent($"""
                Analyze this tennis video and return a JSON array of observations.
                Each observation must have:
                - stroke: one of Forehand, Backhand, Serve, Volley, Overhead, Footwork, General
                - description: specific technique issue observed
                - severity: Info, Warning, or Critical
                - frameTimestamp: approximate time in video (e.g. "0:05")
                - bodyPart: affected body part or null

                Return ONLY a valid JSON array. No other text.
                """)
        ]);

        history.Add(userMessage);

        var sw = Stopwatch.StartNew();
        try
        {
            logger.LogInformation("[VideoAnalysis] Sending video to LLM for analysis (AnalyzeVideo). FileUri={FileUri}", fileUri);
            var response = await chatService.GetChatMessageContentAsync(history, kernel: kernel);
            sw.Stop();

            var content = response.Content ?? "[]";
            logger.LogInformation(
                "[VideoAnalysis] LLM response received in {ElapsedMs}ms. ResponseLength={ResponseLength}",
                sw.ElapsedMilliseconds, content.Length);
            logger.LogDebug("[VideoAnalysis] Observations JSON preview: {Preview}",
                content[..Math.Min(500, content.Length)]);

            return content;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex,
                "[VideoAnalysis] LLM call failed after {ElapsedMs}ms. FileUri={FileUri}",
                sw.ElapsedMilliseconds, fileUri);
            throw;
        }
    }
}
