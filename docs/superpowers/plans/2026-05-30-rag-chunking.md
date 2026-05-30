# RAG Chunking Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the single-embedding-per-report approach with section-based chunking (one vector per observation/recommendation/summary/NtrpEvidence item) and decouple embedding from the video analysis pipeline via a `Channel<Guid>` + `BackgroundService`.

**Architecture:** `TennisCoachOrchestrator` writes the completed report's ID to a bounded `Channel<Guid>` (fire-and-forget) and immediately sets `VideoStatus = Processed`. A singleton `ReportEmbeddingBackgroundService` drains the channel, chunks each report via `IReportChunker`, embeds each chunk, and persists them. The `SearchSimilarAsync` query is updated to deduplicate across multiple chunks per report.

**Tech Stack:** .NET 10, C# 13, EF Core 10, Npgsql, System.Threading.Channels (BCL, no new package), xUnit, Moq

---

## File Map

| Action | Path | Responsibility |
|--------|------|----------------|
| Create | `src/AISportCoach.Domain/Enums/ChunkType.cs` | New enum |
| Create | `src/AISportCoach.Application/Models/ReportChunk.cs` | Value object |
| Create | `src/AISportCoach.Application/Interfaces/IReportChunker.cs` | Interface |
| Create | `src/AISportCoach.Application/Services/ReportChunker.cs` | Pure chunking logic |
| Create | `src/AISportCoach.Infrastructure/BackgroundServices/ReportEmbeddingBackgroundService.cs` | Channel consumer |
| Create | `tests/AISportCoach.IntegrationTests/Unit/ReportChunkerTests.cs` | Unit tests |
| Create | `tests/AISportCoach.IntegrationTests/Unit/ReportEmbeddingBackgroundServiceTests.cs` | Unit tests |
| Modify | `src/AISportCoach.Domain/Entities/ReportEmbedding.cs` | Add ChunkType + ChunkId |
| Modify | `src/AISportCoach.Application/Interfaces/IReportEmbeddingRepository.cs` | Replace AddAsync → AddChunksAsync |
| Modify | `src/AISportCoach.Application/Interfaces/ICoachingReportRepository.cs` | Add GetWithDetailsAsync |
| Modify | `src/AISportCoach.Infrastructure/Persistence/Configurations/ReportEmbeddingConfiguration.cs` | Map new columns |
| Modify | `src/AISportCoach.Infrastructure/Persistence/Repositories/ReportEmbeddingRepository.cs` | AddChunksAsync + new SQL |
| Modify | `src/AISportCoach.Infrastructure/Persistence/Repositories/CoachingReportRepository.cs` | Add GetWithDetailsAsync |
| Modify | `src/AISportCoach.Infrastructure/DependencyInjection.cs` | Channel + hosted service |
| Modify | `src/AISportCoach.Application/Agents/TennisCoachOrchestrator.cs` | ChannelWriter, remove inline embed |
| Modify | `tests/AISportCoach.IntegrationTests/Integration/EmbeddingServiceTest.cs` | Add chunking integration test |
| Generate + edit | `src/AISportCoach.Infrastructure/Migrations/<timestamp>_AddReportEmbeddingChunks.cs` | Schema migration |

---

## Task 1: Domain types — ChunkType enum + ReportChunk value object + IReportChunker interface

**Files:**
- Create: `src/AISportCoach.Domain/Enums/ChunkType.cs`
- Create: `src/AISportCoach.Application/Models/ReportChunk.cs`
- Create: `src/AISportCoach.Application/Interfaces/IReportChunker.cs`

These contain zero logic — no tests needed here.

- [ ] **Step 1: Create ChunkType enum**

```csharp
// src/AISportCoach.Domain/Enums/ChunkType.cs
namespace AISportCoach.Domain.Enums;

public enum ChunkType
{
    Summary        = 1,
    Observation    = 2,
    Recommendation = 3,
    NtrpEvidence   = 4,
}
```

- [ ] **Step 2: Create ReportChunk value object**

```csharp
// src/AISportCoach.Application/Models/ReportChunk.cs
using AISportCoach.Domain.Enums;

namespace AISportCoach.Application.Models;

public record ReportChunk(
    ChunkType ChunkType,
    Guid ChunkId,   // Id of source entity; equals ReportId for Summary chunks
    Guid ReportId,
    string Text);   // max 2048 chars — truncated + warning logged if exceeded
```

- [ ] **Step 3: Create IReportChunker interface**

```csharp
// src/AISportCoach.Application/Interfaces/IReportChunker.cs
using AISportCoach.Application.Models;
using AISportCoach.Domain.Entities;

namespace AISportCoach.Application.Interfaces;

public interface IReportChunker
{
    IReadOnlyList<ReportChunk> Chunk(CoachingReport report);
}
```

- [ ] **Step 4: Verify build**

```
dotnet build src/AISportCoach.Application/AISportCoach.Application.csproj
```

Expected: no errors.

- [ ] **Step 5: Commit**

```
git add src/AISportCoach.Domain/Enums/ChunkType.cs src/AISportCoach.Application/Models/ReportChunk.cs src/AISportCoach.Application/Interfaces/IReportChunker.cs
git commit -m "feat: add ChunkType enum, ReportChunk value object, IReportChunker interface"
```

---

## Task 2: ReportChunker implementation (TDD)

**Files:**
- Create: `tests/AISportCoach.IntegrationTests/Unit/ReportChunkerTests.cs`
- Create: `src/AISportCoach.Application/Services/ReportChunker.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/AISportCoach.IntegrationTests/Unit/ReportChunkerTests.cs
using AISportCoach.Application.Interfaces;
using AISportCoach.Application.Services;
using AISportCoach.Domain.Entities;
using AISportCoach.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace AISportCoach.IntegrationTests.Unit;

public class ReportChunkerTests
{
    private readonly IReportChunker _sut = new ReportChunker(NullLogger<ReportChunker>.Instance);

    private static CoachingReport BuildReport(
        int observationCount = 2,
        int recommendationCount = 1,
        int evidenceCount = 1,
        string summary = "Player has good footwork.")
    {
        var observations = Enumerable.Range(0, observationCount)
            .Select(_ => new TechniqueObservation
            {
                Id = Guid.NewGuid(),
                Stroke = TennisStroke.Forehand,
                Severity = SeverityLevel.Warning,
                Description = "Late contact",
                FrameTimestamp = "00:01:23",
                BodyPart = "Arm"
            })
            .ToList();

        var recommendations = Enumerable.Range(0, recommendationCount)
            .Select(_ => new ImprovementRecommendation
            {
                Id = Guid.NewGuid(),
                Title = "Improve topspin",
                DetailedDescription = "Focus on brushing up on the ball.",
                Priority = 1,
                TargetStroke = TennisStroke.Forehand,
                DrillSuggestions = ["Shadow swing drill", "Ball machine drill"]
            })
            .ToList();

        var evidence = Enumerable.Range(0, evidenceCount)
            .Select(_ => new NtrpEvidence
            {
                Id = Guid.NewGuid(),
                Observation = "Consistent crosscourt shots",
                NtrpIndicator = "Shot consistency",
                SupportedLevel = 3.5,
                Weight = "medium"
            })
            .ToList();

        return CoachingReport.Create(
            Guid.NewGuid(), 75, summary,
            observations, recommendations,
            ntrpRating: 3.5, ntrpEvidence: evidence);
    }

    [Fact]
    public void Chunk_TypicalReport_ProducesCorrectCount()
    {
        var report = BuildReport(observationCount: 2, recommendationCount: 1, evidenceCount: 1);

        var chunks = _sut.Chunk(report);

        // 1 summary + 2 observations + 1 recommendation + 1 evidence = 5
        Assert.Equal(5, chunks.Count);
    }

    [Fact]
    public void Chunk_FirstChunkIsSummaryWithReportId()
    {
        var report = BuildReport();

        var chunks = _sut.Chunk(report);

        Assert.Equal(ChunkType.Summary, chunks[0].ChunkType);
        Assert.Equal(report.Id, chunks[0].ChunkId);
        Assert.Equal(report.Id, chunks[0].ReportId);
        Assert.Contains("Player has good footwork", chunks[0].Text);
    }

    [Fact]
    public void Chunk_ObservationChunks_HaveCorrectTypeAndChunkId()
    {
        var report = BuildReport(observationCount: 2, recommendationCount: 0, evidenceCount: 0);
        var expectedIds = report.Observations.Select(o => o.Id).ToHashSet();

        var chunks = _sut.Chunk(report);
        var obsChunks = chunks.Where(c => c.ChunkType == ChunkType.Observation).ToList();

        Assert.Equal(2, obsChunks.Count);
        Assert.All(obsChunks, c => Assert.Contains(c.ChunkId, expectedIds));
        Assert.All(obsChunks, c => Assert.Contains("Forehand", c.Text));
    }

    [Fact]
    public void Chunk_RecommendationChunks_HaveCorrectTypeAndIncludeDrills()
    {
        var report = BuildReport(observationCount: 0, recommendationCount: 1, evidenceCount: 0);

        var chunks = _sut.Chunk(report);
        var recChunk = Assert.Single(chunks.Where(c => c.ChunkType == ChunkType.Recommendation));

        Assert.Equal(report.Recommendations[0].Id, recChunk.ChunkId);
        Assert.Contains("Shadow swing drill", recChunk.Text);
    }

    [Fact]
    public void Chunk_NtrpEvidenceChunks_HaveCorrectTypeAndContent()
    {
        var report = BuildReport(observationCount: 0, recommendationCount: 0, evidenceCount: 1);

        var chunks = _sut.Chunk(report);
        var evChunk = Assert.Single(chunks.Where(c => c.ChunkType == ChunkType.NtrpEvidence));

        Assert.Equal(report.NtrpEvidence[0].Id, evChunk.ChunkId);
        Assert.Contains("Shot consistency", evChunk.Text);
    }

    [Fact]
    public void Chunk_NoObservationsNoRecommendationsNoEvidence_OnlySummaryChunk()
    {
        var report = BuildReport(observationCount: 0, recommendationCount: 0, evidenceCount: 0);

        var chunks = _sut.Chunk(report);

        Assert.Single(chunks);
        Assert.Equal(ChunkType.Summary, chunks[0].ChunkType);
    }

    [Fact]
    public void Chunk_TextExceeding2048Chars_TruncatesToExactly2048()
    {
        var longSummary = new string('x', 3000);
        var report = BuildReport(observationCount: 0, recommendationCount: 0, evidenceCount: 0,
            summary: longSummary);

        var chunks = _sut.Chunk(report);

        Assert.Equal(2048, chunks[0].Text.Length);
        Assert.EndsWith("...", chunks[0].Text);
    }

    [Fact]
    public void Chunk_AllChunksHaveCorrectReportId()
    {
        var report = BuildReport(observationCount: 1, recommendationCount: 1, evidenceCount: 1);

        var chunks = _sut.Chunk(report);

        Assert.All(chunks, c => Assert.Equal(report.Id, c.ReportId));
    }
}
```

- [ ] **Step 2: Run tests — verify they fail**

```
dotnet test tests/AISportCoach.IntegrationTests --filter "FullyQualifiedName~ReportChunkerTests"
```

Expected: compilation error because `ReportChunker` does not exist yet.

- [ ] **Step 3: Implement ReportChunker**

```csharp
// src/AISportCoach.Application/Services/ReportChunker.cs
using AISportCoach.Application.Interfaces;
using AISportCoach.Application.Models;
using AISportCoach.Domain.Entities;
using AISportCoach.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace AISportCoach.Application.Services;

public class ReportChunker(ILogger<ReportChunker> logger) : IReportChunker
{
    private const int MaxChunkLength = 2048;

    public IReadOnlyList<ReportChunk> Chunk(CoachingReport report)
    {
        var chunks = new List<ReportChunk>();

        chunks.Add(MakeChunk(ChunkType.Summary, report.Id, report.Id,
            $"SUMMARY: {report.ExecutiveSummary}"));

        foreach (var obs in report.Observations)
        {
            var bodyPart = obs.BodyPart is not null ? $" | {obs.BodyPart}" : string.Empty;
            chunks.Add(MakeChunk(ChunkType.Observation, obs.Id, report.Id,
                $"OBSERVATION | {obs.Stroke} | {obs.Severity}{bodyPart}: {obs.Description} | @{obs.FrameTimestamp}"));
        }

        foreach (var rec in report.Recommendations)
        {
            var drills = rec.DrillSuggestions.Count > 0
                ? $"\nDRILLS: {string.Join("; ", rec.DrillSuggestions)}"
                : string.Empty;
            chunks.Add(MakeChunk(ChunkType.Recommendation, rec.Id, report.Id,
                $"RECOMMENDATION | [{rec.Priority}] {rec.TargetStroke} — {rec.Title}: {rec.DetailedDescription}{drills}"));
        }

        foreach (var ev in report.NtrpEvidence)
        {
            chunks.Add(MakeChunk(ChunkType.NtrpEvidence, ev.Id, report.Id,
                $"NTRP EVIDENCE | {ev.NtrpIndicator} level {ev.SupportedLevel:0.0} ({ev.Weight}): {ev.Observation}"));
        }

        return chunks;
    }

    private ReportChunk MakeChunk(ChunkType chunkType, Guid chunkId, Guid reportId, string text)
    {
        if (text.Length > MaxChunkLength)
        {
            logger.LogWarning(
                "Chunk text truncated. ChunkType={ChunkType}, ChunkId={ChunkId}, OriginalLength={Length}",
                chunkType, chunkId, text.Length);
            text = text[..(MaxChunkLength - 3)] + "...";
        }
        return new ReportChunk(chunkType, chunkId, reportId, text);
    }
}
```

- [ ] **Step 4: Run tests — verify they pass**

```
dotnet test tests/AISportCoach.IntegrationTests --filter "FullyQualifiedName~ReportChunkerTests"
```

Expected: all 7 tests pass.

- [ ] **Step 5: Commit**

```
git add src/AISportCoach.Application/Services/ReportChunker.cs tests/AISportCoach.IntegrationTests/Unit/ReportChunkerTests.cs
git commit -m "feat: implement ReportChunker with section-based chunking and 2048-char guard"
```

---

## Task 3: Update domain entity + application interfaces

**Files:**
- Modify: `src/AISportCoach.Domain/Entities/ReportEmbedding.cs`
- Modify: `src/AISportCoach.Application/Interfaces/IReportEmbeddingRepository.cs`
- Modify: `src/AISportCoach.Application/Interfaces/ICoachingReportRepository.cs`

- [ ] **Step 1: Add ChunkType and ChunkId to ReportEmbedding**

Replace the entire content of `src/AISportCoach.Domain/Entities/ReportEmbedding.cs`:

```csharp
using AISportCoach.Domain.Enums;

namespace AISportCoach.Domain.Entities;

public class ReportEmbedding : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid CoachingReportId { get; private set; }
    public Guid UserId { get; private set; }
    public ChunkType ChunkType { get; private set; }
    public Guid ChunkId { get; private set; }
    public float[] Embedding { get; private set; } = [];
    public CoachingReport CoachingReport { get; private set; } = null!;

    private ReportEmbedding() { }

    public static ReportEmbedding Create(
        Guid reportId, Guid userId, ChunkType chunkType, Guid chunkId, float[] embedding) => new()
    {
        Id = Guid.CreateVersion7(),
        CoachingReportId = reportId,
        UserId = userId,
        ChunkType = chunkType,
        ChunkId = chunkId,
        Embedding = embedding,
    };
}
```

- [ ] **Step 2: Replace AddAsync with AddChunksAsync in IReportEmbeddingRepository**

Replace the entire content of `src/AISportCoach.Application/Interfaces/IReportEmbeddingRepository.cs`:

```csharp
using AISportCoach.Application.Models;
using AISportCoach.Domain.Entities;

namespace AISportCoach.Application.Interfaces;

public interface IReportEmbeddingRepository
{
    Task AddChunksAsync(
        Guid userId,
        IReadOnlyList<(ReportChunk Chunk, float[] Embedding)> chunks,
        CancellationToken ct);

    Task<List<CoachingReport>> SearchSimilarAsync(
        float[] queryEmbedding, Guid userId, int topK, double maxDistance, CancellationToken ct);
}
```

- [ ] **Step 3: Add GetWithDetailsAsync to ICoachingReportRepository**

Add one method after the existing `GetByIdAndUserAsync` line in `src/AISportCoach.Application/Interfaces/ICoachingReportRepository.cs`:

```csharp
using AISportCoach.Application.DTOs;
using AISportCoach.Application.Models;
using AISportCoach.Domain.Entities;

namespace AISportCoach.Application.Interfaces;

public interface ICoachingReportRepository
{
    Task<CoachingReport?> GetByIdAsync(Guid reportId, CancellationToken ct = default);
    Task<CoachingReport?> GetByIdAndUserAsync(Guid reportId, Guid userId, CancellationToken ct = default);
    Task<CoachingReport?> GetWithDetailsAsync(Guid reportId, CancellationToken ct = default);
    Task<PagedResult<CoachingReport>> GetPagedAsync(int page, int pageSize, CancellationToken ct = default);
    Task<PagedResult<CoachingReport>> GetPagedByUserAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);
    Task<PagedResult<CoachingReportSummary>> GetPagedSummariesAsync(int page, int pageSize, CancellationToken ct = default);
    Task<PagedResult<CoachingReportSummary>> GetPagedSummariesByUserAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(CoachingReport report, CancellationToken ct = default);
}
```

- [ ] **Step 4: Build — expect compile errors in Infrastructure (not yet updated)**

```
dotnet build src/AISportCoach.Application/AISportCoach.Application.csproj
```

Expected: Application builds. Infrastructure will fail until Task 4.

- [ ] **Step 5: Commit**

```
git add src/AISportCoach.Domain/Entities/ReportEmbedding.cs src/AISportCoach.Application/Interfaces/IReportEmbeddingRepository.cs src/AISportCoach.Application/Interfaces/ICoachingReportRepository.cs
git commit -m "feat: add ChunkType/ChunkId to ReportEmbedding, AddChunksAsync interface, GetWithDetailsAsync interface"
```

---

## Task 4: Update Infrastructure — EF config + repositories

**Files:**
- Modify: `src/AISportCoach.Infrastructure/Persistence/Configurations/ReportEmbeddingConfiguration.cs`
- Modify: `src/AISportCoach.Infrastructure/Persistence/Repositories/ReportEmbeddingRepository.cs`
- Modify: `src/AISportCoach.Infrastructure/Persistence/Repositories/CoachingReportRepository.cs`

- [ ] **Step 1: Update ReportEmbeddingConfiguration**

Replace the entire file content:

```csharp
using AISportCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AISportCoach.Infrastructure.Persistence.Configurations;

public class ReportEmbeddingConfiguration : IEntityTypeConfiguration<ReportEmbedding>
{
    public void Configure(EntityTypeBuilder<ReportEmbedding> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.UserId).IsRequired();
        builder.Property(e => e.ChunkType).HasConversion<string>().IsRequired();
        builder.Property(e => e.ChunkId).IsRequired();

        // The Embedding column is vector(768) — managed via raw SQL in the repository
        // because Npgsql EF Core 10 does not expose a UseVector() hook compatible with
        // the Aspire AddNpgsqlDbContext integration. The column is added in the migration.
        builder.Ignore(e => e.Embedding);

        builder.HasOne(e => e.CoachingReport)
            .WithMany()
            .HasForeignKey(e => e.CoachingReportId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

- [ ] **Step 2: Update ReportEmbeddingRepository — AddChunksAsync + new SearchSimilarAsync SQL**

Replace the entire file content:

```csharp
using System.Globalization;
using AISportCoach.Application.Interfaces;
using AISportCoach.Application.Models;
using AISportCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AISportCoach.Infrastructure.Persistence.Repositories;

public class ReportEmbeddingRepository(AppDbContext context) : IReportEmbeddingRepository
{
    private static string ToVectorLiteral(float[] values) =>
        $"[{string.Join(",", values.Select(f => f.ToString(CultureInfo.InvariantCulture)))}]";

    public async Task AddChunksAsync(
        Guid userId,
        IReadOnlyList<(ReportChunk Chunk, float[] Embedding)> chunks,
        CancellationToken ct)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        var createdAt = DateTime.UtcNow;

        foreach (var (chunk, embedding) in chunks)
        {
            var vectorLiteral = ToVectorLiteral(embedding);
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO "ReportEmbeddings" ("Id", "CoachingReportId", "UserId", "ChunkType", "ChunkId", "Embedding", "CreatedAt")
                VALUES (@id, @reportId, @userId, @chunkType, @chunkId, @embedding::vector, @createdAt)
                """,
                [
                    new NpgsqlParameter("id", Guid.CreateVersion7()),
                    new NpgsqlParameter("reportId", chunk.ReportId),
                    new NpgsqlParameter("userId", userId),
                    new NpgsqlParameter("chunkType", chunk.ChunkType.ToString()),
                    new NpgsqlParameter("chunkId", chunk.ChunkId),
                    new NpgsqlParameter("embedding", vectorLiteral),
                    new NpgsqlParameter("createdAt", createdAt)
                ],
                ct);
        }

        await transaction.CommitAsync(ct);
    }

    public async Task<List<CoachingReport>> SearchSimilarAsync(
        float[] queryEmbedding, Guid userId, int topK, double maxDistance, CancellationToken ct)
    {
        var vectorLiteral = ToVectorLiteral(queryEmbedding);

        // CTE finds the TopK reports with the closest-matching chunk (deduplicates across chunks).
        var sql = $"""
            WITH ranked AS (
                SELECT "CoachingReportId",
                       MIN("Embedding" <=> '{vectorLiteral}'::vector) AS min_distance
                FROM "ReportEmbeddings"
                WHERE "UserId" = @userId
                  AND "Embedding" <=> '{vectorLiteral}'::vector < @maxDistance
                GROUP BY "CoachingReportId"
                ORDER BY min_distance
                LIMIT @topK
            )
            SELECT cr.* FROM "CoachingReports" cr
            JOIN ranked ON ranked."CoachingReportId" = cr."Id"
            ORDER BY ranked.min_distance
            """;

        return await context.CoachingReports
            .FromSqlRaw(sql,
                new NpgsqlParameter("userId", userId),
                new NpgsqlParameter<double>("maxDistance", maxDistance),
                new NpgsqlParameter("topK", topK))
            .AsNoTracking()
            .Include(r => r.Observations)
            .ToListAsync(ct);
    }
}
```

- [ ] **Step 3: Add GetWithDetailsAsync to CoachingReportRepository**

Add this method to `src/AISportCoach.Infrastructure/Persistence/Repositories/CoachingReportRepository.cs` after `GetByIdAndUserAsync`:

```csharp
    public Task<CoachingReport?> GetWithDetailsAsync(Guid reportId, CancellationToken ct = default)
        => db.CoachingReports
             .AsNoTracking()
             .Include(r => r.VideoUpload)
             .Include(r => r.Observations)
             .Include(r => r.Recommendations)
             .Include(r => r.NtrpEvidence)
             .FirstOrDefaultAsync(r => r.Id == reportId, ct);
```

- [ ] **Step 4: Build Infrastructure to verify no compile errors**

```
dotnet build src/AISportCoach.Infrastructure/AISportCoach.Infrastructure.csproj
```

Expected: builds cleanly (TennisCoachOrchestrator in Application still uses old `AddAsync` — it will be fixed in Task 6).

- [ ] **Step 5: Commit**

```
git add src/AISportCoach.Infrastructure/Persistence/Configurations/ReportEmbeddingConfiguration.cs src/AISportCoach.Infrastructure/Persistence/Repositories/ReportEmbeddingRepository.cs src/AISportCoach.Infrastructure/Persistence/Repositories/CoachingReportRepository.cs
git commit -m "feat: update ReportEmbeddingRepository to AddChunksAsync with CTE dedup, add GetWithDetailsAsync"
```

---

## Task 5: EF Core migration

**Files:**
- Generate + edit: `src/AISportCoach.Infrastructure/Migrations/<timestamp>_AddReportEmbeddingChunks.cs`

- [ ] **Step 1: Generate migration from solution root**

```
dotnet ef migrations add AddReportEmbeddingChunks --project src/AISportCoach.Infrastructure --startup-project src/AISportCoach.API
```

Expected: a new migration file is created in `src/AISportCoach.Infrastructure/Migrations/`.

- [ ] **Step 2: Edit the generated migration — add DELETE before AddColumn**

Open the generated `*_AddReportEmbeddingChunks.cs` file. The generated `Up` method will contain two `AddColumn` calls. Prepend a `Sql` call to delete stale rows first. The final `Up` method must look like:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // Existing single-vector embeddings are stale — clear before adding NOT NULL columns.
    migrationBuilder.Sql("""DELETE FROM "ReportEmbeddings";""");

    migrationBuilder.AddColumn<string>(
        name: "ChunkType",
        table: "ReportEmbeddings",
        type: "text",
        nullable: false,
        defaultValue: "");

    migrationBuilder.AddColumn<Guid>(
        name: "ChunkId",
        table: "ReportEmbeddings",
        type: "uuid",
        nullable: false,
        defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
}
```

Leave `Down` as EF generated it (it will call `DropColumn` for both).

- [ ] **Step 3: Build to verify migration compiles**

```
dotnet build src/AISportCoach.Infrastructure/AISportCoach.Infrastructure.csproj
```

Expected: no errors.

- [ ] **Step 4: Commit**

```
git add src/AISportCoach.Infrastructure/Migrations/
git commit -m "feat: add EF migration for ChunkType and ChunkId columns on ReportEmbeddings"
```

---

## Task 6: BackgroundService + channel wiring + orchestrator update

**Files:**
- Create: `src/AISportCoach.Infrastructure/BackgroundServices/ReportEmbeddingBackgroundService.cs`
- Modify: `src/AISportCoach.Infrastructure/DependencyInjection.cs`
- Modify: `src/AISportCoach.Application/Agents/TennisCoachOrchestrator.cs`

- [ ] **Step 1: Create ReportEmbeddingBackgroundService**

```csharp
// src/AISportCoach.Infrastructure/BackgroundServices/ReportEmbeddingBackgroundService.cs
using AISportCoach.Application.Interfaces;
using AISportCoach.Application.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace AISportCoach.Infrastructure.BackgroundServices;

public class ReportEmbeddingBackgroundService(
    ChannelReader<Guid> channelReader,
    IServiceScopeFactory scopeFactory,
    IReportChunker chunker,
    ILogger<ReportEmbeddingBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var reportId in channelReader.ReadAllAsync(stoppingToken))
        {
            using var scope = scopeFactory.CreateScope();
            var reportRepository = scope.ServiceProvider.GetRequiredService<ICoachingReportRepository>();
            var embeddingService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
            var embeddingRepository = scope.ServiceProvider.GetRequiredService<IReportEmbeddingRepository>();

            try
            {
                var report = await reportRepository.GetWithDetailsAsync(reportId, stoppingToken);
                if (report is null)
                {
                    logger.LogWarning("Report {ReportId} not found for embedding — skipping", reportId);
                    continue;
                }

                var userId = report.VideoUpload.UserId;
                var chunks = chunker.Chunk(report);
                var pairs = new List<(ReportChunk, float[])>(chunks.Count);

                foreach (var chunk in chunks)
                {
                    var embedding = await embeddingService
                        .GenerateEmbeddingAsync(chunk.Text, EmbeddingTaskType.Document, stoppingToken);
                    pairs.Add((chunk, embedding));
                }

                await embeddingRepository.AddChunksAsync(userId, pairs, stoppingToken);
                logger.LogInformation(
                    "[Embedding] Saved {ChunkCount} chunks for report {ReportId}", pairs.Count, reportId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Embedding] Pipeline failed for report {ReportId}", reportId);
                // swallowed — video stays Processed
            }
        }
    }
}
```

- [ ] **Step 2: Register channel and background service in DependencyInjection.cs**

Add the following lines to `AddInfrastructure`, just before `return services;`:

```csharp
        // Embedding channel — bounded, fire-and-forget from orchestrator to background service
        services.AddSingleton<System.Threading.Channels.Channel<Guid>>(_ =>
            System.Threading.Channels.Channel.CreateBounded<Guid>(
                new System.Threading.Channels.BoundedChannelOptions(capacity: 100)
                {
                    FullMode = System.Threading.Channels.BoundedChannelFullMode.DropWrite,
                    SingleWriter = false,
                    SingleReader = true,
                }));
        services.AddSingleton(sp =>
            sp.GetRequiredService<System.Threading.Channels.Channel<Guid>>().Writer);
        services.AddSingleton(sp =>
            sp.GetRequiredService<System.Threading.Channels.Channel<Guid>>().Reader);

        // IReportChunker is pure logic — singleton is safe
        services.AddSingleton<IReportChunker, ReportChunker>();

        services.AddHostedService<ReportEmbeddingBackgroundService>();
```

Add the missing using directives at the top of `DependencyInjection.cs`:

```csharp
using AISportCoach.Application.Services;
using AISportCoach.Infrastructure.BackgroundServices;
```

- [ ] **Step 3: Update TennisCoachOrchestrator — inject ChannelWriter, remove inline embedding**

Replace the entire content of `src/AISportCoach.Application/Agents/TennisCoachOrchestrator.cs`:

```csharp
using AISportCoach.Application.Interfaces;
using AISportCoach.Application.Plugins;
using AISportCoach.Domain.Entities;
using AISportCoach.Domain.Enums;
using AISportCoach.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.Text.Json;
using System.Threading.Channels;

namespace AISportCoach.Application.Agents;

public class TennisCoachOrchestrator(
    Kernel kernel,
    VideoAnalysisPlugin videoAnalysisPlugin,
    ReportGenerationPlugin reportGenerationPlugin,
    ICoachingReportRepository reportRepository,
    IVideoRepository videoRepository,
    IVideoFileService videoFileService,
    ChannelWriter<Guid> embeddingChannel,
    ILogger<TennisCoachOrchestrator> logger)
{
    public async Task<CoachingReport> ProcessAsync(
        Guid videoId,
        IReadOnlySet<AnalysisScope> scopes,
        CancellationToken cancellationToken)
    {
        var video = await videoRepository.GetByIdAsync(videoId, cancellationToken)
            ?? throw new VideoNotFoundException(videoId);

        try
        {
            var fileUri = video.GeminiFileUri
                ?? throw new InvalidOperationException($"Video {video.Id} has no Gemini file URI.");

            logger.LogInformation(
                "[Orchestrator] Starting analysis for video: {VideoId}, FileUri: {FileUri}, Scopes: {Scopes}",
                video.Id, fileUri, string.Join(",", scopes));

            await videoFileService.WaitForFileActiveAsync(fileUri, cancellationToken);

            logger.LogInformation("[Orchestrator] Step 1/2 — video analysis. VideoId={VideoId}, FileUri={FileUri}", videoId, fileUri);
            var mergedJson = await videoAnalysisPlugin.AnalyzeVideoAsync(kernel, fileUri, scopes);
            var (observationsJson, ntrpJson) = SplitMergedAnalysisJson(mergedJson, scopes.Contains(AnalysisScope.Ntrp));
            logger.LogInformation("[Orchestrator] Step 1/2 complete. ObservationsJsonLength={Length}", observationsJson.Length);

            logger.LogInformation("[Orchestrator] Step 2/2 — report generation. VideoId={VideoId}", videoId);
            var reportJson = await reportGenerationPlugin.GenerateCoachingReportAsync(
                kernel, observationsJson, null, ntrpJson);
            logger.LogInformation("[Orchestrator] Step 2/2 complete. ReportJsonLength={Length}", reportJson.Length);

            if (string.IsNullOrWhiteSpace(reportJson))
                throw new InvalidOperationException("ReportGenerationPlugin returned empty report");

            var report = ParseAndSaveReport(videoId, reportJson, ntrpJson ?? string.Empty);
            logger.LogInformation(
                "[Orchestrator] Report parsed. VideoId={VideoId}, Score={Score}, NtrpRating={NtrpRating}, Observations={ObsCount}, Recommendations={RecCount}",
                videoId, report.OverallScore, report.NtrpRating, report.Observations.Count, report.Recommendations.Count);

            await reportRepository.AddAsync(report, cancellationToken);

            if (!embeddingChannel.TryWrite(report.Id))
                logger.LogWarning("[Embedding] Channel full — report {ReportId} will not be embedded", report.Id);

            video.SetStatus(VideoStatus.Processed);
            logger.LogInformation("[Orchestrator] Analysis for video {VideoId} completed successfully.", videoId);

            await videoRepository.UpdateAsync(video, cancellationToken);
            return report;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Orchestrator] Analysis failed for video {VideoId}.", videoId);
            video.SetStatus(VideoStatus.Failed);
            await videoRepository.UpdateAsync(video, cancellationToken);
            throw;
        }
    }

    private (string observationsJson, string? ntrpJson) SplitMergedAnalysisJson(string mergedJson, bool expectNtrp)
    {
        mergedJson = StripToJson(mergedJson, '{', '}');
        try
        {
            using var doc = JsonDocument.Parse(mergedJson);
            var root = doc.RootElement;

            var observationsJson = root.TryGetProperty("observations", out var obsEl)
                ? obsEl.GetRawText()
                : "[]";

            string? ntrpJson = null;
            if (expectNtrp && root.TryGetProperty("ntrp", out var ntrpEl)
                           && ntrpEl.ValueKind == JsonValueKind.Object)
                ntrpJson = ntrpEl.GetRawText();
            else if (expectNtrp)
                logger.LogWarning("[Orchestrator] NTRP requested but 'ntrp' key missing from LLM response.");

            return (observationsJson, ntrpJson);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "[Orchestrator] Failed to split merged JSON — falling back to empty observations.");
            return ("[]", null);
        }
    }

    private CoachingReport ParseAndSaveReport(Guid videoId, string reportJson, string ntrpJson)
    {
        reportJson = StripToJson(reportJson, '{', '}');
        ntrpJson   = StripToJson(ntrpJson,   '{', '}');

        using var reportDoc = JsonDocument.Parse(reportJson);
        var root = reportDoc.RootElement;

        var overallScore = root.TryGetProperty("overallScore", out var scoreEl) ? scoreEl.GetInt32() : 50;
        var summary = root.TryGetProperty("executiveSummary", out var summaryEl) ? summaryEl.GetString() ?? "" : "";

        var observations = new List<TechniqueObservation>();
        if (root.TryGetProperty("observations", out var obsArr))
        {
            foreach (var obs in obsArr.EnumerateArray())
            {
                observations.Add(new TechniqueObservation
                {
                    Id = Guid.CreateVersion7(),
                    Stroke = ParseEnum<TennisStroke>(obs, "stroke"),
                    Description = obs.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
                    Severity = ParseEnum<SeverityLevel>(obs, "severity"),
                    FrameTimestamp = obs.TryGetProperty("frameTimestamp", out var ft) ? ft.GetString() ?? "" : "",
                    BodyPart = obs.TryGetProperty("bodyPart", out var bp) ? bp.GetString() : null
                });
            }
        }

        var recommendations = new List<ImprovementRecommendation>();
        if (root.TryGetProperty("recommendations", out var recArr))
        {
            foreach (var rec in recArr.EnumerateArray())
            {
                var drills = new List<string>();
                if (rec.TryGetProperty("drillSuggestions", out var drillsEl))
                    foreach (var drill in drillsEl.EnumerateArray())
                        drills.Add(drill.GetString() ?? "");

                recommendations.Add(new ImprovementRecommendation
                {
                    Id = Guid.CreateVersion7(),
                    Title = rec.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                    DetailedDescription = rec.TryGetProperty("detailedDescription", out var dd) ? dd.GetString() ?? "" : "",
                    Priority = rec.TryGetProperty("priority", out var p) ? p.GetInt32() : 1,
                    TargetStroke = ParseEnum<TennisStroke>(rec, "targetStroke"),
                    DrillSuggestions = drills
                });
            }
        }

        double? ntrpRating = null;
        double? ntrpMin = null;
        double? ntrpMax = null;
        string? ntrpConfidence = null;
        string? ntrpJustification = null;
        var ntrpEvidenceList = new List<NtrpEvidence>();

        try
        {
            using var ntrpDoc = JsonDocument.Parse(ntrpJson);
            var ntrp = ntrpDoc.RootElement;

            if (ntrp.TryGetProperty("ntrpRating", out var ratingEl))
                ntrpRating = ratingEl.GetDouble();

            if (ntrp.TryGetProperty("ntrpRatingRange", out var rangeEl))
            {
                if (rangeEl.TryGetProperty("min", out var minEl)) ntrpMin = minEl.GetDouble();
                if (rangeEl.TryGetProperty("max", out var maxEl)) ntrpMax = maxEl.GetDouble();
            }

            if (ntrp.TryGetProperty("confidence", out var confEl))
                ntrpConfidence = confEl.GetString();

            if (ntrp.TryGetProperty("ratingJustification", out var justEl))
                ntrpJustification = justEl.GetString();

            if (ntrp.TryGetProperty("evidence", out var evidenceArr))
            {
                foreach (var ev in evidenceArr.EnumerateArray())
                {
                    ntrpEvidenceList.Add(new NtrpEvidence
                    {
                        Id = Guid.CreateVersion7(),
                        Observation = ev.TryGetProperty("observation", out var obs) ? obs.GetString() ?? "" : "",
                        NtrpIndicator = ev.TryGetProperty("ntrpIndicator", out var ind) ? ind.GetString() ?? "" : "",
                        SupportedLevel = ev.TryGetProperty("supportedLevel", out var lvl) ? lvl.GetDouble() : 0,
                        Weight = ev.TryGetProperty("weight", out var w) ? w.GetString() ?? "" : ""
                    });
                }
            }
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "[Orchestrator] Failed to parse NTRP JSON — report will be saved without NTRP data.");
        }

        return CoachingReport.Create(videoId, overallScore, summary,
            observations, recommendations,
            ntrpRating, ntrpMin, ntrpMax, ntrpConfidence, ntrpJustification, ntrpEvidenceList);
    }

    private static string StripToJson(string text, char open, char close)
    {
        var start = text.IndexOf(open);
        var end   = text.LastIndexOf(close);
        return start >= 0 && end > start ? text[start..(end + 1)] : text;
    }

    private static T ParseEnum<T>(JsonElement element, string propertyName) where T : struct, Enum
    {
        if (element.TryGetProperty(propertyName, out var prop) &&
            Enum.TryParse<T>(prop.GetString(), true, out var result))
            return result;
        return default;
    }
}
```

- [ ] **Step 4: Build entire solution**

```
dotnet build
```

Expected: no errors.

- [ ] **Step 5: Commit**

```
git add src/AISportCoach.Infrastructure/BackgroundServices/ReportEmbeddingBackgroundService.cs src/AISportCoach.Infrastructure/DependencyInjection.cs src/AISportCoach.Application/Agents/TennisCoachOrchestrator.cs
git commit -m "feat: add ReportEmbeddingBackgroundService, wire Channel<Guid>, remove inline embedding from orchestrator"
```

---

## Task 7: Background service unit tests

**Files:**
- Create: `tests/AISportCoach.IntegrationTests/Unit/ReportEmbeddingBackgroundServiceTests.cs`

- [ ] **Step 1: Write tests**

```csharp
// tests/AISportCoach.IntegrationTests/Unit/ReportEmbeddingBackgroundServiceTests.cs
using AISportCoach.Application.Interfaces;
using AISportCoach.Application.Models;
using AISportCoach.Application.Services;
using AISportCoach.Domain.Entities;
using AISportCoach.Domain.Enums;
using AISportCoach.Infrastructure.BackgroundServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Threading.Channels;

namespace AISportCoach.IntegrationTests.Unit;

public class ReportEmbeddingBackgroundServiceTests
{
    private static CoachingReport BuildReport()
    {
        var observations = new List<TechniqueObservation>
        {
            new() { Id = Guid.NewGuid(), Stroke = TennisStroke.Forehand, Severity = SeverityLevel.Warning,
                    Description = "Late contact", FrameTimestamp = "00:01:00" }
        };
        var report = CoachingReport.Create(Guid.NewGuid(), 70, "Good footwork", observations, []);
        // Simulate VideoUpload navigation property being loaded
        var videoUpload = CreateVideoUpload(Guid.NewGuid(), report.VideoUploadId);
        SetVideoUpload(report, videoUpload);
        return report;
    }

    private static VideoUpload CreateVideoUpload(Guid userId, Guid videoId)
    {
        // VideoUpload has a private constructor; use reflection to set UserId
        var upload = (VideoUpload)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(VideoUpload));
        typeof(VideoUpload).GetProperty("Id")!.SetValue(upload, videoId);
        typeof(VideoUpload).GetProperty("UserId")!.SetValue(upload, userId);
        return upload;
    }

    private static void SetVideoUpload(CoachingReport report, VideoUpload upload)
    {
        typeof(CoachingReport).GetProperty("VideoUpload")!.SetValue(report, upload);
    }

    private static ReportEmbeddingBackgroundService BuildService(
        ChannelReader<Guid> reader,
        ICoachingReportRepository reportRepo,
        IEmbeddingService embeddingService,
        IReportEmbeddingRepository embeddingRepo)
    {
        var services = new ServiceCollection();
        services.AddSingleton(reportRepo);
        services.AddSingleton(embeddingService);
        services.AddSingleton(embeddingRepo);
        var scopeFactory = services.BuildServiceProvider();

        return new ReportEmbeddingBackgroundService(
            reader,
            scopeFactory,
            new ReportChunker(NullLogger<ReportChunker>.Instance),
            NullLogger<ReportEmbeddingBackgroundService>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_ValidReport_CallsAddChunksAsyncWithCorrectChunkCount()
    {
        var report = BuildReport();
        var reportId = report.Id;
        // 1 summary + 1 observation = 2 chunks
        var expectedChunkCount = 2;

        var channel = Channel.CreateUnbounded<Guid>();
        channel.Writer.TryWrite(reportId);
        channel.Writer.Complete();

        var mockReportRepo = new Mock<ICoachingReportRepository>();
        mockReportRepo.Setup(r => r.GetWithDetailsAsync(reportId, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(report);

        var mockEmbeddingService = new Mock<IEmbeddingService>();
        mockEmbeddingService.Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), EmbeddingTaskType.Document, It.IsAny<CancellationToken>()))
                            .ReturnsAsync(new float[768]);

        var mockEmbeddingRepo = new Mock<IReportEmbeddingRepository>();

        var sut = BuildService(channel.Reader, mockReportRepo.Object, mockEmbeddingService.Object, mockEmbeddingRepo.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await sut.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(500), cts.Token);
        await sut.StopAsync(CancellationToken.None);

        mockEmbeddingRepo.Verify(r => r.AddChunksAsync(
            It.IsAny<Guid>(),
            It.Is<IReadOnlyList<(ReportChunk, float[])>>(l => l.Count == expectedChunkCount),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ReportNotFound_SkipsAndContinues()
    {
        var channel = Channel.CreateUnbounded<Guid>();
        channel.Writer.TryWrite(Guid.NewGuid());
        channel.Writer.Complete();

        var mockReportRepo = new Mock<ICoachingReportRepository>();
        mockReportRepo.Setup(r => r.GetWithDetailsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                      .ReturnsAsync((CoachingReport?)null);

        var mockEmbeddingService = new Mock<IEmbeddingService>();
        var mockEmbeddingRepo = new Mock<IReportEmbeddingRepository>();

        var sut = BuildService(channel.Reader, mockReportRepo.Object, mockEmbeddingService.Object, mockEmbeddingRepo.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await sut.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(500), cts.Token);
        await sut.StopAsync(CancellationToken.None);

        mockEmbeddingRepo.Verify(r => r.AddChunksAsync(
            It.IsAny<Guid>(), It.IsAny<IReadOnlyList<(ReportChunk, float[])>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_EmbeddingServiceThrows_SwallowsAndContinues()
    {
        var report = BuildReport();
        var channel = Channel.CreateUnbounded<Guid>();
        channel.Writer.TryWrite(report.Id);
        channel.Writer.Complete();

        var mockReportRepo = new Mock<ICoachingReportRepository>();
        mockReportRepo.Setup(r => r.GetWithDetailsAsync(report.Id, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(report);

        var mockEmbeddingService = new Mock<IEmbeddingService>();
        mockEmbeddingService.Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<EmbeddingTaskType>(), It.IsAny<CancellationToken>()))
                            .ThrowsAsync(new HttpRequestException("Gemini API unavailable"));

        var mockEmbeddingRepo = new Mock<IReportEmbeddingRepository>();

        var sut = BuildService(channel.Reader, mockReportRepo.Object, mockEmbeddingService.Object, mockEmbeddingRepo.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        // Should not throw
        await sut.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(500), cts.Token);
        await sut.StopAsync(CancellationToken.None);

        mockEmbeddingRepo.Verify(r => r.AddChunksAsync(
            It.IsAny<Guid>(), It.IsAny<IReadOnlyList<(ReportChunk, float[])>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
```

- [ ] **Step 2: Run tests**

```
dotnet test tests/AISportCoach.IntegrationTests --filter "FullyQualifiedName~ReportEmbeddingBackgroundServiceTests"
```

Expected: all 3 tests pass. Note: `VideoUpload` may have a public `UserId` property that is settable — if reflection fails, check the `VideoUpload` entity and adjust `CreateVideoUpload` to use its actual constructor or `Create` factory.

- [ ] **Step 3: Commit**

```
git add tests/AISportCoach.IntegrationTests/Unit/ReportEmbeddingBackgroundServiceTests.cs
git commit -m "test: add unit tests for ReportEmbeddingBackgroundService"
```

---

## Task 8: Integration test — chunking + embedding end-to-end

**Files:**
- Modify: `tests/AISportCoach.IntegrationTests/Integration/EmbeddingServiceTest.cs`

- [ ] **Step 1: Add the chunking integration test to EmbeddingServiceTest.cs**

Append this test class after the existing `EmbeddingServiceTest` class in the same file:

```csharp
public class ReportChunkingIntegrationTest
{
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
    public async Task ChunkAndEmbed_ReportWith2ObsAnd1Rec_Returns4VectorsOf768Dims()
    {
        var apiKey = ReadApiKey();

        var services = new ServiceCollection();
        services.AddGoogleAIEmbeddingGenerator(
            modelId: "gemini-embedding-001",
            apiKey: apiKey,
            apiVersion: GoogleAIVersion.V1_Beta,
            dimensions: 768);

        var sp = services.BuildServiceProvider();
        var generator = sp.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        var embeddingService = new GeminiEmbeddingService(generator);
        var chunker = new ReportChunker(Microsoft.Extensions.Logging.Abstractions.NullLogger<ReportChunker>.Instance);

        var observations = new List<TechniqueObservation>
        {
            new() { Id = Guid.NewGuid(), Stroke = TennisStroke.Forehand, Severity = SeverityLevel.Warning,
                    Description = "Late contact point, racket face too open at impact.",
                    FrameTimestamp = "00:01:05", BodyPart = "Wrist" },
            new() { Id = Guid.NewGuid(), Stroke = TennisStroke.Backhand, Severity = SeverityLevel.Critical,
                    Description = "Shortened backswing with early deceleration.",
                    FrameTimestamp = "00:02:15", BodyPart = "Shoulder" }
        };
        var recommendations = new List<ImprovementRecommendation>
        {
            new() { Id = Guid.NewGuid(), Title = "Improve topspin", Priority = 1,
                    TargetStroke = TennisStroke.Forehand,
                    DetailedDescription = "Focus on low-to-high swing path.",
                    DrillSuggestions = ["Shadow swing", "Slow motion practice"] }
        };
        var report = CoachingReport.Create(
            Guid.NewGuid(), 65,
            "Player shows inconsistency across groundstrokes due to poor swing mechanics.",
            observations, recommendations);

        var chunks = chunker.Chunk(report);
        // 1 summary + 2 observations + 1 recommendation = 4
        Assert.Equal(4, chunks.Count);

        foreach (var chunk in chunks)
        {
            var vector = await embeddingService.GenerateEmbeddingAsync(
                chunk.Text, EmbeddingTaskType.Document, CancellationToken.None);

            Assert.Equal(768, vector.Length);
            Assert.Contains(vector, v => v != 0f);
        }
    }
}
```

Also add the missing usings at the top of `EmbeddingServiceTest.cs` if not already present:

```csharp
using AISportCoach.Application.Services;
using AISportCoach.Domain.Entities;
using AISportCoach.Domain.Enums;
using AISportCoach.Infrastructure.Services;
```

- [ ] **Step 2: Run the integration test (requires Gemini API key)**

```
dotnet test tests/AISportCoach.IntegrationTests --filter "FullyQualifiedName~ReportChunkingIntegrationTest"
```

Expected: all 4 embedding calls succeed; each vector is 768-dim with non-zero values.

- [ ] **Step 3: Commit**

```
git add tests/AISportCoach.IntegrationTests/Integration/EmbeddingServiceTest.cs
git commit -m "test: add chunking integration test — chunk report into 4 sections and embed each"
```

---

## Self-Review Checklist

**Spec coverage:**
- ✅ `ChunkType` enum (Task 1)
- ✅ `ReportChunk` value object (Task 1)
- ✅ `IReportChunker` + `ReportChunker` (Tasks 1–2)
- ✅ `IReportEmbeddingRepository.AddChunksAsync` (Task 3)
- ✅ `ICoachingReportRepository.GetWithDetailsAsync` (Task 3)
- ✅ `ReportEmbedding` entity extended (Task 3)
- ✅ EF config + repositories updated (Task 4)
- ✅ Migration with DELETE + AddColumn (Task 5)
- ✅ `ReportEmbeddingBackgroundService` + channel + scope factory (Task 6)
- ✅ `TennisCoachOrchestrator` updated (Task 6)
- ✅ `ReportChunkerTests` unit tests (Task 2)
- ✅ `ReportEmbeddingBackgroundServiceTests` unit tests (Task 7)
- ✅ Chunking integration test (Task 8)

**Deviations from spec (intentional):**
- `AddChunksAsync` takes `Guid userId` as first parameter (spec omitted it; required because `ReportChunk` has no `UserId` and the repository needs it for the INSERT)
- `ICoachingReportRepository.GetWithDetailsAsync` includes `VideoUpload` navigation (needed to get `UserId` for embedding storage)
- `IServiceScopeFactory` used in background service (spec showed direct injection; scope factory is required because the service is singleton but repositories are scoped)
