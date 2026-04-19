#pragma warning disable SKEXP0070

using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AISportCoach.IntegrationTests.Integration;

/// <summary>
/// Sanity check: verifies the Gemini API key is valid and the model responds.
/// No video upload — just a simple text prompt.
/// </summary>
public class GeminiConnectivityTest
{
    private const string Model = "gemini-2.5-flash";

    private static string ReadApiKey()
    {
        var fromEnv = Environment.GetEnvironmentVariable("Gemini__ApiKey");
        if (!string.IsNullOrWhiteSpace(fromEnv)) return fromEnv;

        foreach (var fileName in new[] { "secrets.json", "appsettings.test.json" })
        {
            var path = Path.Combine(AppContext.BaseDirectory, fileName);
            if (!File.Exists(path)) continue;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("Gemini", out var gemini) &&
                gemini.TryGetProperty("ApiKey", out var key))
            {
                var value = key.GetString();
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
        }

        throw new InvalidOperationException("Gemini:ApiKey not set.");
    }

    [Fact]
    public async Task Gemini_SimpleTextPrompt_ReturnsResponse()
    {
        var apiKey = ReadApiKey();

        var kernel = Kernel.CreateBuilder()
            .AddGoogleAIGeminiChatCompletion(Model, apiKey)
            .Build();

        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        history.AddUserMessage("Reply with exactly one word: tennis");

        var response = await chatService.GetChatMessageContentAsync(history, kernel: kernel);

        Assert.NotNull(response);
        Assert.NotEmpty(response.Content ?? "");
    }
}
