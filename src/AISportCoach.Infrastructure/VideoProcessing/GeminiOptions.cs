namespace AISportCoach.Infrastructure.VideoProcessing;

public record GeminiOptions
{
    public string ApiKey { get; init; } = "";
    public string ModelId { get; init; } = "gemini-3-flash-preview";
    public string BaseUrl { get; init; } = "https://generativelanguage.googleapis.com";
    public int HttpTimeoutMinutes { get; init; } = 10;
}
