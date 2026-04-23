using AISportCoach.Application.Interfaces;
using Microsoft.Extensions.AI;

namespace AISportCoach.Infrastructure.Services;

public class GeminiEmbeddingService(IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator) : IEmbeddingService
{
    public async Task<float[]> GenerateEmbeddingAsync(string text, EmbeddingTaskType taskType, CancellationToken ct)
    {
        var options = new EmbeddingGenerationOptions
        {
            AdditionalProperties = new() { ["taskType"] = taskType == EmbeddingTaskType.Document
                ? "RETRIEVAL_DOCUMENT" : "RETRIEVAL_QUERY" }
        };
        var embeddings = await embeddingGenerator.GenerateAsync([text], options, ct);
        return embeddings[0].Vector.ToArray();
    }
}
