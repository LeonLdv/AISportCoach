using AISportCoach.Application.Interfaces;
using AISportCoach.Application.Plugins;
using AISportCoach.Domain.Entities;
using AISportCoach.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.Text;
using System.Text.Json;

namespace AISportCoach.Application.Agents;

public class TennisCoachOrchestrator(
    Kernel kernel,
    VideoAnalysisPlugin videoAnalysisPlugin,
    ReportGenerationPlugin reportGenerationPlugin,
    NtrpRatingPlugin ntrpRatingPlugin,
    ICoachingReportRepository reportRepository,
    IVideoRepository videoRepository,
    IReportEmbeddingRepository embeddingRepository,
    IEmbeddingService embeddingService,
    ILogger<TennisCoachOrchestrator> logger)
{
    public async Task<CoachingReport> ProcessAsync(Guid videoId, CancellationToken cancellationToken)
    {
        var video = await videoRepository.GetByIdAsync(videoId, cancellationToken)
            ?? throw new VideoNotFoundException(videoId);

        try
        {
            var fileUri = video.GeminiFileUri
                ?? throw new InvalidOperationException($"Video {video.Id} has no Gemini file URI.");

            logger.LogInformation(
                "[Orchestrator] Starting analysis for video: {VideoId}, fileUri: {FileUri}",
                video.Id, fileUri);

            // Step 1 — video analysis
            logger.LogInformation("[Orchestrator] Step 1/3 — video analysis. VideoId={VideoId}, FileUri={FileUri}", videoId, fileUri);
            var observationsJson = await videoAnalysisPlugin.AnalyzeVideoAsync(kernel, fileUri);
            logger.LogInformation("[Orchestrator] Step 1/3 complete. ObservationsJsonLength={Length}", observationsJson.Length);

            // Retrieve history context for history-aware report generation
            var historySummary = await FetchHistorySummaryAsync(observationsJson, video.UserId, cancellationToken);
            logger.LogInformation(
                "[History] Retrieved similar past sessions. HasHistory={HasHistory}",
                historySummary is not null);

            // Step 2 — coaching report (with optional history context)
            logger.LogInformation("[Orchestrator] Step 2/3 — report generation. VideoId={VideoId}", videoId);
            var reportJson = await reportGenerationPlugin.GenerateCoachingReportAsync(
                kernel, observationsJson, historySummary);
            logger.LogInformation("[Orchestrator] Step 2/3 complete. ReportJsonLength={Length}", reportJson.Length);

            if (string.IsNullOrWhiteSpace(reportJson))
                throw new InvalidOperationException("ReportGenerationPlugin returned empty report");

            // Step 3 — NTRP rating
            logger.LogInformation("[Orchestrator] Step 3/3 — NTRP rating. VideoId={VideoId}", videoId);
            var ntrpJson = await ntrpRatingPlugin.DetermineNtrpRatingAsync(kernel, observationsJson);
            logger.LogInformation("[Orchestrator] Step 3/3 complete. NtrpJsonLength={Length}", ntrpJson.Length);

            var report = ParseAndSaveReport(videoId, reportJson, ntrpJson);
            logger.LogInformation(
                "[Orchestrator] Report parsed. VideoId={VideoId}, Score={Score}, NtrpRating={NtrpRating}, Observations={ObsCount}, Recommendations={RecCount}",
                videoId, report.OverallScore, report.NtrpRating, report.Observations.Count, report.Recommendations.Count);

            await reportRepository.AddAsync(report, cancellationToken);

            // Generate and save embedding for future history retrieval
            var embeddingText = BuildEmbeddingText(report);
            var vector = await embeddingService.GenerateEmbeddingAsync(embeddingText, cancellationToken);
            await embeddingRepository.AddAsync(ReportEmbedding.Create(report.Id, video.UserId, vector), cancellationToken);
            logger.LogInformation("[Embedding] Saved report embedding {ReportId}.", report.Id);

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

    private async Task<string?> FetchHistorySummaryAsync(string observationsJson, Guid userId, CancellationToken ct)
    {
        try
        {
            var queryVector = await embeddingService.GenerateEmbeddingAsync(observationsJson, ct);
            var pastReports = await embeddingRepository.SearchSimilarAsync(queryVector, userId, topK: 5, ct);

            if (pastReports.Count == 0)
                return null;

            var sb = new StringBuilder();
            foreach (var report in pastReports.OrderBy(r => r.GeneratedAt))
            {
                sb.AppendLine($"Session {report.GeneratedAt:yyyy-MM-dd} | NTRP {report.NtrpRating?.ToString("0.0") ?? "N/A"} | Score {report.OverallScore}/100");
                sb.AppendLine($"Summary: \"{report.ExecutiveSummary}\"");

                var critical = report.Observations.Where(o => o.Severity == SeverityLevel.Critical).ToList();
                var warnings = report.Observations.Where(o => o.Severity == SeverityLevel.Warning).ToList();

                if (critical.Count > 0)
                    sb.AppendLine($"Critical: {string.Join("; ", critical.Select(o => $"{o.Stroke} — \"{o.Description}\""))}");
                if (warnings.Count > 0)
                    sb.AppendLine($"Warning: {string.Join("; ", warnings.Select(o => $"{o.Stroke} — \"{o.Description}\""))}");

                sb.AppendLine();
            }
            return sb.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            // History retrieval is best-effort; do not block the main pipeline
            logger.LogWarning(ex, "[History] Failed to retrieve past sessions — continuing without history context.");
            return null;
        }
    }

    private static string BuildEmbeddingText(CoachingReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine(report.ExecutiveSummary);
        if (report.NtrpRating.HasValue)
            sb.AppendLine($"NTRP {report.NtrpRating:0.0} ({report.NtrpRatingMin:0.0}–{report.NtrpRatingMax:0.0}, {report.NtrpConfidence} confidence)");
        if (report.NtrpRatingJustification is not null)
            sb.AppendLine(report.NtrpRatingJustification);
        foreach (var obs in report.Observations)
            sb.AppendLine($"{obs.Stroke} {obs.Severity}: {obs.Description}");
        foreach (var rec in report.Recommendations)
            sb.AppendLine(rec.Title);
        return sb.ToString();
    }

    private CoachingReport ParseAndSaveReport(Guid videoId, string reportJson, string ntrpJson)
    {
        // Strip markdown fences
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

        // Parse NTRP
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
