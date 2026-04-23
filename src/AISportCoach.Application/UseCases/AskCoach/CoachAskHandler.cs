using AISportCoach.Application.Interfaces;
using AISportCoach.Application.Options;
using AISportCoach.Application.Plugins;
using AISportCoach.Domain.Constants;
using AISportCoach.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using System.Text;
using System.Text.Json;

namespace AISportCoach.Application.UseCases.AskCoach;

public class CoachAskHandler(
    IEmbeddingService embeddingService,
    IReportEmbeddingRepository embeddingRepository,
    IOptions<RagOptions> ragOptions,
    CoachQAPlugin coachQAPlugin,
    Kernel kernel,
    ILogger<CoachAskHandler> logger) : IRequestHandler<CoachAskQuery, CoachAnswerResult>
{
    public async Task<CoachAnswerResult> Handle(CoachAskQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation("[CoachAsk] Received question. QuestionLength={Length}", request.Question.Length);

        var questionVector = await embeddingService.GenerateEmbeddingAsync(request.Question, EmbeddingTaskType.Query, cancellationToken);

        var similarReports = await embeddingRepository.SearchSimilarAsync(
            questionVector, MockUser.Id, ragOptions.Value.TopK, ragOptions.Value.SimilarityThreshold, cancellationToken);

        var historyContext = similarReports.Count == 0
            ? "No previous sessions found."
            : FormatHistoryContext(similarReports);

        logger.LogInformation("[CoachAsk] Retrieved {Count} similar past sessions for Q&A context.", similarReports.Count);

        var rawJson = await coachQAPlugin.AnswerQuestionAsync(kernel, request.Question, historyContext);
        rawJson = StripToJson(rawJson, '{', '}');

        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;

        var answer = root.TryGetProperty("answer", out var answerEl) ? answerEl.GetString() ?? "" : "";
        var advice = root.TryGetProperty("advice", out var adviceEl) ? adviceEl.GetString() ?? "" : "";
        var drills = new List<string>();
        
        if (root.TryGetProperty("drills", out var drillsEl))
        {
            foreach (var drill in drillsEl.EnumerateArray())
            {
                drills.Add(drill.GetString() ?? "");
            }
        }

        return new CoachAnswerResult(answer, advice, drills);
    }

    private static string FormatHistoryContext(IEnumerable<CoachingReport> reports)
    {
        var sb = new StringBuilder();
        foreach (var report in reports.OrderBy(r => r.CreatedAt))
        {
            sb.AppendLine($"Session {report.CreatedAt:yyyy-MM-dd} | NTRP {report.NtrpRating?.ToString("0.0") ?? "N/A"} | Score {report.OverallScore}/100");
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

    private static string StripToJson(string text, char open, char close)
    {
        var start = text.IndexOf(open);
        var end = text.LastIndexOf(close);
        return start >= 0 && end > start ? text[start..(end + 1)] : text;
    }
}
