using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.ComponentModel;
using System.Diagnostics;

namespace AISportCoach.Application.Plugins;

public class ReportGenerationPlugin(ILogger<ReportGenerationPlugin> logger)
{
    [KernelFunction("GenerateCoachingReport")]
    [Description("Generates a structured coaching report with improvement recommendations based on technique analysis")]
    public async Task<string> GenerateCoachingReportAsync(
        Kernel kernel,
        [Description("JSON object with classified observations and score from TechniqueEvaluationPlugin")] string techniqueAnalysisJson,
        [Description("Optional summary of the player's past sessions for trend analysis")] string? playerHistorySummary = null)
    {
        logger.LogInformation(
            "[ReportGeneration] Generating coaching report. InputJsonLength={InputLength}, HasHistory={HasHistory}",
            techniqueAnalysisJson.Length, playerHistorySummary is not null);
        logger.LogDebug("[ReportGeneration] Input JSON preview: {Preview}",
            techniqueAnalysisJson[..Math.Min(300, techniqueAnalysisJson.Length)]);

        var historyBlock = playerHistorySummary is not null
            ? $"""

<PlayerHistory>
Below are the player's recent sessions (oldest → newest).
Identify confirmed improvements, recurring issues, and any regression.
Weave trend observations into executiveSummary and recommendations.

{playerHistorySummary}
</PlayerHistory>

"""
            : string.Empty;

        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var prompt = "You are a professional tennis coach. Based on this technique analysis, generate a comprehensive coaching report.\n\n" +
                     $"Analysis: {techniqueAnalysisJson}\n" +
                     historyBlock +
                     "\nReturn a JSON object with:\n" +
                     "- overallScore: 0-100\n" +
                     "- executiveSummary: 2-3 sentence summary of the player's technique\n" +
                     "- observations: array of {stroke, description, severity, frameTimestamp, bodyPart}\n" +
                     "- recommendations: array of {title, detailedDescription, priority(1=highest), targetStroke, drillSuggestions(array of strings)}\n\n" +
                     "Return ONLY valid JSON. No other text.";

        var sw = Stopwatch.StartNew();
        try
        {
            logger.LogInformation("[ReportGeneration] Sending request to LLM (GenerateCoachingReport)");
            var response = await chatService.GetChatMessageContentAsync(prompt, kernel: kernel);
            sw.Stop();

            var content = response.Content ?? "{}";
            logger.LogInformation(
                "[ReportGeneration] LLM response received in {ElapsedMs}ms. ResponseLength={ResponseLength}",
                sw.ElapsedMilliseconds, content.Length);
            logger.LogDebug("[ReportGeneration] Response preview: {Preview}",
                content[..Math.Min(500, content.Length)]);

            return content;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex,
                "[ReportGeneration] LLM call failed after {ElapsedMs}ms.",
                sw.ElapsedMilliseconds);
            throw;
        }
    }
}
