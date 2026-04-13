using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.ComponentModel;
using System.Diagnostics;

namespace AISportCoach.Application.Plugins;

public class CoachQAPlugin(ILogger<CoachQAPlugin> logger)
{
    [KernelFunction("AnswerQuestion")]
    [Description("Answers a player's tennis coaching question grounded in their session history")]
    public async Task<string> AnswerQuestionAsync(
        Kernel kernel,
        [Description("The player's natural-language question")] string question,
        [Description("Formatted summary of the player's past sessions")] string historyContext)
    {
        logger.LogInformation(
            "[CoachQA] Answering question. QuestionLength={QuestionLength}, HistoryLength={HistoryLength}",
            question.Length, historyContext.Length);

        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var prompt = "You are a professional tennis coach with access to a player's session history.\n\n" +
                     $"Player's session history:\n{historyContext}\n\n" +
                     $"Player's question: {question}\n\n" +
                     "Based on the player's history, provide a personalised coaching response.\n" +
                     "Return a JSON object with:\n" +
                     "- answer: direct answer to the question (1-3 sentences)\n" +
                     "- advice: specific coaching advice tailored to the player's observed weaknesses\n" +
                     "- drills: array of 2-4 drill suggestions as strings\n\n" +
                     "Return ONLY valid JSON. No other text.";

        var sw = Stopwatch.StartNew();
        try
        {
            logger.LogInformation("[CoachQA] Sending request to LLM (AnswerQuestion)");
            var response = await chatService.GetChatMessageContentAsync(prompt, kernel: kernel);
            sw.Stop();

            var content = response.Content ?? "{}";
            logger.LogInformation(
                "[CoachQA] LLM response received in {ElapsedMs}ms. ResponseLength={ResponseLength}",
                sw.ElapsedMilliseconds, content.Length);

            return content;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex, "[CoachQA] LLM call failed after {ElapsedMs}ms.", sw.ElapsedMilliseconds);
            throw;
        }
    }
}
