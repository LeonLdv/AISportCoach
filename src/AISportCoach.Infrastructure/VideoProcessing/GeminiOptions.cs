namespace AISportCoach.Infrastructure.VideoProcessing;

public record GeminiOptions
{
    public string ApiKey { get; init; } = "";
    public string ModelId { get; init; } = "gemini-2.5-flash";
    public string BaseUrl { get; init; } = "https://generativelanguage.googleapis.com";
}
