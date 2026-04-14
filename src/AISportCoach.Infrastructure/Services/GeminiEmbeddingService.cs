using AISportCoach.Application.Interfaces;
using Microsoft.SemanticKernel.Embeddings;

namespace AISportCoach.Infrastructure.Services;

#pragma warning disable SKEXP0001
public class GeminiEmbeddingService(ITextEmbeddingGenerationService embeddingService) : IEmbeddingService
{
    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct)
    {
        var embedding = await embeddingService.GenerateEmbeddingAsync(text, cancellationToken: ct);
        return embedding.ToArray();
    }
}
#pragma warning restore SKEXP0001
