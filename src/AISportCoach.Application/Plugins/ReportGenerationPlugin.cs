using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.ComponentModel;

namespace AISportCoach.Application.Plugins;

public class ReportGenerationPlugin(ILogger<ReportGenerationPlugin> logger)
{
    [KernelFunction("GenerateCoachingReport")]
    [Description("Generates a structured coaching report with improvement recommendations based on technique analysis")]
    public async Task<string> GenerateCoachingReportAsync(
        Kernel kernel,
        [Description("JSON object with classified observations and score from TechniqueEvaluationPlugin")] string techniqueAnalysisJson,
        [Description("Optional summary of the player's past sessions for trend analysis")] string? playerHistorySummary = null,
        [Description("Optional NTRP rating JSON produced by NtrpRatingPlugin")] string? ntrpJson = null)
    {
        logger.LogInformation(
            "[ReportGeneration] Generating coaching report. InputJsonLength={InputLength}, HasHistory={HasHistory}, HasNtrp={HasNtrp}",
            techniqueAnalysisJson.Length, playerHistorySummary is not null, ntrpJson is not null);
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

        var ntrpBlock = ntrpJson is not null
            ? $"""

<NtrpRating>
The player's NTRP rating has been independently assessed. Use this to calibrate the difficulty and language of your recommendations.

{ntrpJson}
</NtrpRating>

"""
            : string.Empty;

        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var prompt = "You are a professional tennis coach. Based on this technique analysis, generate a comprehensive coaching report.\n\n" +
                     $"Analysis: {techniqueAnalysisJson}\n" +
                     ntrpBlock +
                     historyBlock +
                     "\nReturn a JSON object with:\n" +
                     "- overallScore: 0-100\n" +
                     "- executiveSummary: 2-3 sentence summary of the player's technique\n" +
                     "- observations: array of {stroke, description, severity, frameTimestamp, bodyPart}\n" +
                     "- recommendations: array of {title, detailedDescription, priority(1=highest), targetStroke, drillSuggestions(array of strings)}\n\n" +
                     "Return ONLY valid JSON. No other text.";

        try
        {
            logger.LogInformation("[ReportGeneration] Sending request to LLM (GenerateCoachingReport)");
            var response = await chatService.GetChatMessageContentAsync(prompt, kernel: kernel);

            var content = response.Content ?? "{}";
            logger.LogInformation(
                "[ReportGeneration] LLM response received. ResponseLength={ResponseLength}",
                content.Length);
            logger.LogDebug("[ReportGeneration] Response preview: {Preview}",
                content[..Math.Min(500, content.Length)]);

            return content;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[ReportGeneration] LLM call failed.");
            throw;
        }
    }
}
