namespace AISportCoach.Application.Options;

public record RagOptions
{
    public int TopK { get; init; } = 5;
    public double SimilarityThreshold { get; init; } = 0.5; // cosine distance ceiling (lower = more similar)
}
