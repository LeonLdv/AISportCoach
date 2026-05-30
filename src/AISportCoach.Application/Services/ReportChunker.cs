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
