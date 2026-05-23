using AISportCoach.Domain.Entities;
using AISportCoach.Domain.Enums;
using System.Text;

namespace AISportCoach.Application.Agents.Helpers;

internal static class RagContextFormatter
{
    internal static string Format(List<CoachingReport> reports, TennisStroke? strokeFilter)
    {
        var sb = new StringBuilder();

        foreach (var report in reports.OrderBy(r => r.CreatedAt))
        {
            var observations = strokeFilter.HasValue
                ? report.Observations.Where(o => o.Stroke == strokeFilter.Value).ToList()
                : report.Observations.ToList();

            var recommendations = strokeFilter.HasValue
                ? report.Recommendations.Where(r => r.TargetStroke == strokeFilter.Value).ToList()
                : report.Recommendations.ToList();

            if (strokeFilter.HasValue && observations.Count == 0 && recommendations.Count == 0)
                continue;

            sb.AppendLine($"Session {report.CreatedAt:yyyy-MM-dd} | NTRP {report.NtrpRating?.ToString("0.0") ?? "N/A"} | Score {report.OverallScore}/100");
            sb.AppendLine($"Summary: \"{report.ExecutiveSummary}\"");

            var critical = observations.Where(o => o.Severity == SeverityLevel.Critical).ToList();
            var warnings = observations.Where(o => o.Severity == SeverityLevel.Warning).ToList();

            if (critical.Count > 0)
                sb.AppendLine($"Critical: {string.Join("; ", critical.Select(o => $"{o.Stroke} — \"{o.Description}\""))}");
            if (warnings.Count > 0)
                sb.AppendLine($"Warning: {string.Join("; ", warnings.Select(o => $"{o.Stroke} — \"{o.Description}\""))}");
            if (recommendations.Count > 0)
                sb.AppendLine($"Recommendations: {string.Join("; ", recommendations.Select(r => r.Title))}");

            sb.AppendLine();
        }

        var result = sb.ToString().TrimEnd();
        return string.IsNullOrEmpty(result) ? "No relevant session history found." : result;
    }
}
