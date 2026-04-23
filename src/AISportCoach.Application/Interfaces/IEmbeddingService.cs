namespace AISportCoach.Application.Interfaces;

public enum EmbeddingTaskType { Document, Query }

public interface IEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(string text, EmbeddingTaskType taskType, CancellationToken ct);
}
