# RAG Chunking Design

**Date:** 2026-05-29  
**Status:** Approved  
**Scope:** Section-based chunking of coaching reports, async embedding via C# channel, updated pgvector retrieval

---

## Problem

The current system generates **one embedding per coaching report** (`BuildEmbeddingText` concatenates everything into a single string). This is a RAG anti-pattern — the whole-report vector dilutes retrieval precision. A question about forehand topspin retrieves reports where the forehand is barely mentioned because the embedding captures all strokes equally.

The embedding also runs **inline** inside `TennisCoachOrchestrator.ProcessAsync`, blocking the video from reaching `Processed` status until all embedding calls complete.

---

## Goals

1. Chunk each `CoachingReport` into one vector per semantic section (observation, recommendation, summary, NTRP evidence).
2. Decouple embedding from the analysis pipeline — video reaches `Processed` immediately, embedding runs in the background.
3. Update RAG retrieval to handle multiple chunks per report without duplicating results.
4. No new library dependencies — SK handles embedding, `System.Threading.Channels` handles the pipeline.

---

## Architecture Overview

```
TennisCoachOrchestrator.ProcessAsync()
  │
  ├─ [existing] analyse video → save CoachingReport → set VideoStatus = Processed
  │
  └─ [new] TryWrite(reportId) → Channel<Guid>  (fire-and-forget)

Channel<Guid>  ←── singleton, bounded capacity 100, DropWrite on full
  │
  └─ ReportEmbeddingBackgroundService  (IHostedService)
       │
       ├─ reads ReportId from channel
       ├─ loads CoachingReport with Observations + Recommendations + NtrpEvidence
       ├─ IReportChunker.Chunk() → IReadOnlyList<ReportChunk>
       ├─ foreach chunk: IEmbeddingService.GenerateEmbeddingAsync()
       └─ IReportEmbeddingRepository.AddChunksAsync()

RAG query (flow unchanged, retrieval SQL updated)
  CoachAskHandler → embed question → SearchSimilarAsync() → TopK reports (deduped) → agents
```

**Key invariant:** `TennisCoachOrchestrator` never awaits embedding. Embedding failures are logged and swallowed — the video stays `Processed`.

---

## Domain Layer

**New enum `ChunkType`** in `AISportCoach.Domain/Enums/ChunkType.cs`:

```csharp
public enum ChunkType
{
    Summary        = 1,
    Observation    = 2,
    Recommendation = 3,
    NtrpEvidence   = 4,
}
```

---

## Application Layer

### `ReportChunk` value object

Location: `AISportCoach.Application/Models/ReportChunk.cs`

```csharp
public record ReportChunk(
    ChunkType ChunkType,
    Guid ChunkId,    // Id of source entity; equals ReportId for Summary chunks
    Guid ReportId,
    string Text);    // max 2048 chars — truncated + warning logged if exceeded
```

### `IReportChunker` interface

Location: `AISportCoach.Application/Interfaces/IReportChunker.cs`

```csharp
public interface IReportChunker
{
    IReadOnlyList<ReportChunk> Chunk(CoachingReport report);
}
```

### `ReportChunker` implementation

Location: `AISportCoach.Application/Services/ReportChunker.cs`

Pure logic, no I/O. Produces chunks in this order:

| # | ChunkType | ChunkId | Text content |
|---|-----------|---------|--------------|
| 1 | Summary | ReportId | ExecutiveSummary |
| 2..N | Observation | TechniqueObservation.Id | Stroke + Severity + Description + BodyPart + FrameTimestamp |
| N+1..M | Recommendation | ImprovementRecommendation.Id | Title + DetailedDescription + DrillSuggestions (joined) |
| M+1..K | NtrpEvidence | NtrpEvidence.Id | Observation + NtrpIndicator + SupportedLevel + Weight |

Any chunk whose formatted text exceeds 2048 characters is truncated to `text[..2045] + "..."` and a `LogWarning` is emitted with `ChunkType` and `ChunkId`.

### `IReportEmbeddingRepository` update

Remove `AddAsync(ReportEmbedding embedding, CancellationToken ct)`.  
Add:

```csharp
Task AddChunksAsync(
    IReadOnlyList<(ReportChunk Chunk, float[] Embedding)> chunks,
    CancellationToken ct);
```

`SearchSimilarAsync` signature is unchanged.

---

## Infrastructure Layer

### `ReportEmbedding` entity changes

Add two required properties:

```csharp
public ChunkType ChunkType { get; init; }
public Guid ChunkId { get; init; }
```

### `ReportEmbeddingConfiguration` changes

```csharp
b.Property(e => e.ChunkType).HasConversion<string>().IsRequired();
b.Property(e => e.ChunkId).IsRequired();
```

`Embedding` column remains untracked by EF Core (raw SQL only — existing pattern).

### Migration

One migration with three operations:

```sql
DELETE FROM "ReportEmbeddings";   -- existing single-vector rows are stale
ALTER TABLE "ReportEmbeddings" ADD COLUMN "ChunkType" text NOT NULL;
ALTER TABLE "ReportEmbeddings" ADD COLUMN "ChunkId" uuid NOT NULL;
```

The HNSW index on `Embedding` is unchanged.

### `ReportEmbeddingRepository.AddChunksAsync`

Loops over the list, inserts each row via raw SQL using the existing `::vector` cast pattern. All inserts run inside a single transaction.

### Updated `SearchSimilarAsync` SQL

Deduplicate by report — return the closest-matching chunk per report:

```sql
SELECT DISTINCT ON (r."Id") r.*,
       MIN(e."Embedding" <=> @queryVector) AS distance
FROM "CoachingReports" r
JOIN "ReportEmbeddings" e ON e."CoachingReportId" = r."Id"
WHERE e."UserId" = @userId
  AND e."Embedding" <=> @queryVector < @maxDistance
GROUP BY r."Id"
ORDER BY r."Id", distance
LIMIT @topK
```

A question about forehand ranks a report high if *any* of its chunks (forehand observation, forehand recommendation) is a strong match.

### Channel registration

In `DependencyInjection.cs`:

```csharp
services.AddSingleton<Channel<Guid>>(_ =>
    Channel.CreateBounded<Guid>(new BoundedChannelOptions(capacity: 100)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleWriter = false,
        SingleReader = true,
    }));
services.AddSingleton(sp => sp.GetRequiredService<Channel<Guid>>().Writer);
services.AddSingleton(sp => sp.GetRequiredService<Channel<Guid>>().Reader);
services.AddHostedService<ReportEmbeddingBackgroundService>();
```

### `ReportEmbeddingBackgroundService`

Location: `AISportCoach.Infrastructure/BackgroundServices/ReportEmbeddingBackgroundService.cs`

`GetWithDetailsAsync` must eager-load `Observations`, `Recommendations`, and `NtrpEvidence` — all three collections are required by `IReportChunker`.

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    await foreach (var reportId in _channelReader.ReadAllAsync(stoppingToken))
    {
        try
        {
            var report = await _reportRepository.GetWithDetailsAsync(reportId, stoppingToken);
            if (report is null)
            {
                _logger.LogWarning("Report {ReportId} not found for embedding", reportId);
                continue;
            }

            var chunks = _chunker.Chunk(report);
            var pairs = new List<(ReportChunk, float[])>(chunks.Count);
            foreach (var chunk in chunks)
            {
                var embedding = await _embeddingService
                    .GenerateEmbeddingAsync(chunk.Text, EmbeddingTaskType.Document, stoppingToken);
                pairs.Add((chunk, embedding));
            }

            await _embeddingRepository.AddChunksAsync(pairs, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Embedding pipeline failed for report {ReportId}", reportId);
            // swallowed — video stays Processed
        }
    }
}
```

### `TennisCoachOrchestrator` change

Inject `ChannelWriter<Guid>`. Replace the inline embedding block with:

```csharp
if (!_embeddingChannel.TryWrite(report.Id))
    _logger.LogWarning("Embedding channel full — report {ReportId} will not be embedded", report.Id);
```

Remove injection of `IEmbeddingService` and `IReportEmbeddingRepository` from the orchestrator (they move to the background service).

---

## Error Handling

| Scenario | Behavior |
|---|---|
| Channel full at write time | `TryWrite` → false → `LogWarning`, report stays `Processed` without embedding |
| Report not found by background service | `LogWarning` → skip, loop continues |
| Embedding API call fails | `LogError` → exception swallowed, loop continues |
| Chunk text > 2048 chars | Truncate + `LogWarning` with `ChunkType` and `ChunkId` |
| `AddChunksAsync` DB write fails | `LogError` → exception swallowed |
| App shutdown mid-drain | `stoppingToken` cancels `ReadAllAsync` — in-flight report may have partial embeddings (acceptable) |

No retries in scope. A future periodic re-embedding job for reports with zero chunk embeddings is a possible follow-up.

---

## Testing

### Unit — `ReportChunkerTests`

- Given a `CoachingReport` with N observations, M recommendations, K evidence items:
  - Assert chunk count = 1 + N + M + K
  - Assert `ChunkType` correct per chunk
  - Assert `ChunkId` equals source entity `Id` (and `ReportId` for Summary)
- Given an observation whose text exceeds 2048 chars: assert text is truncated to 2048, ends with `...`

### Unit — `ReportEmbeddingBackgroundServiceTests`

- Mock `ChannelReader<Guid>`, `ICoachingReportRepository`, `IReportChunker`, `IEmbeddingService`, `IReportEmbeddingRepository`
- Assert `AddChunksAsync` called with correct pair count for a report with known chunk count
- Assert exception from `IEmbeddingService` is swallowed and loop continues to next report

### Integration — extend `EmbeddingServiceTest`

- Build a `CoachingReport` with 2 observations + 1 recommendation
- Run `ReportChunker.Chunk()` — assert 4 chunks (1 summary + 2 obs + 1 rec)
- Call `IEmbeddingService.GenerateEmbeddingAsync()` for each chunk
- Assert each embedding is 768-dim with at least one non-zero value

---

## Out of Scope

- Retry / dead-letter queue for failed embeddings
- Passing matched `ChunkType` to `RagContextFormatter` for section highlighting
- Token counting (truncation guard uses character count as a proxy)
- `EmbeddingPending` video status
