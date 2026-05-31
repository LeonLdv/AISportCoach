using AISportCoach.Application.Interfaces;
using AISportCoach.Application.Messages;
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
    ChannelWriter<ReportEmbeddingQueued> embeddingChannel,
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

            if (!embeddingChannel.TryWrite(new ReportEmbeddingQueued(report.Id)))
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
