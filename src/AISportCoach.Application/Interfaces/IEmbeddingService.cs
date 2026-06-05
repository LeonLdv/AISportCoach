namespace AISportCoach.Application.Interfaces;

public enum EmbeddingTaskType { Document, Query }

public interface IEmbeddingService
{
    Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(string text, EmbeddingTaskType taskType, CancellationToken ct);
}
