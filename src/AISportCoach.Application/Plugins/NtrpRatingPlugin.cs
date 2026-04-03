using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.ComponentModel;
using System.Diagnostics;

namespace AISportCoach.Application.Plugins;

public class NtrpRatingPlugin(ILogger<NtrpRatingPlugin> logger)
{
    private const string NtrpScaleDefinitions = """
        NTRP Scale Reference (USTA National Tennis Rating Program):
        1.5 - Beginner: Just starting tennis, no competitive background.
        2.0 - Beginner+: Limited experience, working on sustaining a short rally.
        2.5 - Novice: Learning court positions, limited consistency; comfortable only with slow-paced shots.
        3.0 - Beginner-Intermediate: Consistent on medium-paced shots, beginning to develop strokes but lacks directional control.
        3.5 - Intermediate: Can sustain a rally, directional control on groundstrokes improving; approaches net occasionally.
        4.0 - Intermediate+: Dependable strokes with directional control; can attack short balls, uses spin regularly.
        4.5 - Advanced-Intermediate: Good shot anticipation, consistent on above-average pace; can vary strategy.
        5.0 - Advanced: Strong all-court game; first and second serves with power and placement; good net skills.
        5.5 - Advanced+: Can hit winners or force errors with all strokes; plays at a sectional/national level.
        6.0 - Competitive: Intense tournament competitor; national ranking; professional or near-professional level.
        6.5 - Touring Pro: Full-time professional; consistent prize money earner on ATP/WTA tours.
        7.0 - World Class: Top-ranked ATP/WTA professional player.
        """;

    [KernelFunction("DetermineNtrpRating")]
    [Description("Analyzes tennis technique observations and assigns an evidence-based NTRP rating (1.5–7.0 scale)")]
    public async Task<string> DetermineNtrpRatingAsync(
        Kernel kernel,
        [Description("JSON array of technique observations produced by VideoAnalysisPlugin.AnalyzeVideo")] string observationsJson,
        [Description("Categorical player level hint: Beginner, Intermediate, or Advanced")] string playerLevel = "Intermediate")
    {
        logger.LogInformation(
            "[NtrpRating] Starting NTRP rating determination. PlayerLevel={PlayerLevel}, InputJsonLength={InputLength}",
            playerLevel, observationsJson.Length);
        logger.LogDebug("[NtrpRating] Observations JSON preview: {Preview}",
            observationsJson[..Math.Min(300, observationsJson.Length)]);

        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        var systemPrompt = $$"""
            You are a certified USTA tennis rater with decades of experience assigning NTRP levels.

            {{NtrpScaleDefinitions}}

            Your task:
            1. Review each observation in the provided JSON array.
            2. Map each observation to one or more NTRP indicators.
            3. Derive a consensus NTRP rating with explicit, traceable evidence.
            4. Return ONLY a valid JSON object — no markdown fences, no commentary.

            Output schema:
            {
              "ntrpRating": <number, one of: 1.5, 2.0, 2.5, 3.0, 3.5, 4.0, 4.5, 5.0, 5.5, 6.0, 6.5, 7.0>,
              "ntrpRatingRange": { "min": <lower bound>, "max": <upper bound> },
              "confidence": <"low" | "medium" | "high">,
              "ratingJustification": "<2-4 sentence explanation of the consensus rating>",
              "evidence": [
                {
                  "observation": "<exact description from input>",
                  "ntrpIndicator": "<which NTRP characteristic this maps to>",
                  "supportedLevel": <numeric NTRP level this evidence points to>,
                  "weight": <"low" | "medium" | "high">
                }
              ]
            }
            """;

        var userPrompt = $"""
            Player level hint: {playerLevel}

            Technique observations (JSON array):
            {observationsJson}

            Assign an NTRP rating with full evidence justification. Return ONLY valid JSON.
            """;

        var history = new ChatHistory();
        history.AddSystemMessage(systemPrompt);
        history.AddUserMessage(userPrompt);

        var sw = Stopwatch.StartNew();
        try
        {
            logger.LogInformation("[NtrpRating] Sending request to LLM (DetermineNtrpRating)");
            var response = await chatService.GetChatMessageContentAsync(history, kernel: kernel);
            sw.Stop();

            var content = response.Content ?? "{}";
            logger.LogInformation(
                "[NtrpRating] LLM response received in {ElapsedMs}ms. ResponseLength={ResponseLength}",
                sw.ElapsedMilliseconds, content.Length);
            logger.LogDebug("[NtrpRating] Response preview: {Preview}",
                content[..Math.Min(500, content.Length)]);

            return content;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex,
                "[NtrpRating] LLM call failed after {ElapsedMs}ms. PlayerLevel={PlayerLevel}",
                sw.ElapsedMilliseconds, playerLevel);
            throw;
        }
    }
}
