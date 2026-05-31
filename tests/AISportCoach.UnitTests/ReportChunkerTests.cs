using AISportCoach.Application.Interfaces;
using AISportCoach.Application.Services;
using AISportCoach.Domain.Entities;
using AISportCoach.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace AISportCoach.UnitTests;

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

    [Fact]
    public void Chunk_ObservationWithNullBodyPart_TextDoesNotContainBodyPartSeparator()
    {
        var report = CoachingReport.Create(
            Guid.NewGuid(), 70, "Summary",
            [new TechniqueObservation
            {
                Id = Guid.NewGuid(),
                Stroke = TennisStroke.Serve,
                Severity = SeverityLevel.Info,
                Description = "Ball toss inconsistent",
                FrameTimestamp = "00:00:30",
                BodyPart = null
            }],
            []);

        var chunks = _sut.Chunk(report);
        var obsChunk = chunks.Single(c => c.ChunkType == ChunkType.Observation);

        Assert.Contains("Ball toss inconsistent", obsChunk.Text);
        Assert.DoesNotContain(" | null", obsChunk.Text);
    }
}
