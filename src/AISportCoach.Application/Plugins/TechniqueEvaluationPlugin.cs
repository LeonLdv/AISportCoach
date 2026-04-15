using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.ComponentModel;

namespace AISportCoach.Application.Plugins;

public class TechniqueEvaluationPlugin(ILogger<TechniqueEvaluationPlugin> logger)
{
    [KernelFunction("ClassifyObservations")]
    [Description("Classifies and deduplicates raw frame observations into organized technique analysis by stroke type")]
    public async Task<string> ClassifyObservationsAsync(
        Kernel kernel,
        [Description("JSON array of raw observations from frame analysis")] string rawObservationsJson,
        [Description("Player skill level for context")] string playerLevel = "Intermediate")
    {
        logger.LogInformation(
            "[TechniqueEvaluation] Classifying observations. PlayerLevel={PlayerLevel}, RawJsonLength={InputLength}",
            playerLevel, rawObservationsJson.Length);
        logger.LogDebug("[TechniqueEvaluation] Raw observations preview: {Preview}",
            rawObservationsJson[..Math.Min(300, rawObservationsJson.Length)]);

        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var prompt = "You are a tennis coach. Given these raw observations from video frame analysis, " +
                     "classify, deduplicate, and organize them into a coherent technique analysis.\n\n" +
                     $"Player level: {playerLevel}\n" +
                     $"Raw observations: {rawObservationsJson}\n\n" +
                     "Return a JSON object with:\n" +
                     "- observations: deduplicated array of {stroke, description, severity, frameTimestamp, bodyPart}\n" +
                     "- overallScore: 0-100 score of player's technique\n" +
                     "- playerSkillLevel: confirmed skill level assessment\n\n" +
                     "Return ONLY valid JSON. No other text.";

        try
        {
            logger.LogInformation("[TechniqueEvaluation] Sending request to LLM (ClassifyObservations)");
            var response = await chatService.GetChatMessageContentAsync(prompt, kernel: kernel);

            var content = response.Content ?? """{"observations":[],"overallScore":50,"playerSkillLevel":"Intermediate"}""";
            logger.LogInformation(
                "[TechniqueEvaluation] LLM response received. ResponseLength={ResponseLength}",
                content.Length);
            logger.LogDebug("[TechniqueEvaluation] Classified analysis preview: {Preview}",
                content[..Math.Min(500, content.Length)]);

            return content;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[TechniqueEvaluation] LLM call failed. PlayerLevel={PlayerLevel}", playerLevel);
            throw;
        }
    }
}
