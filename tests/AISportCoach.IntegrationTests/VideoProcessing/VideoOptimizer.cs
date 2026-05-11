using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using FFMpegCore;
using FFMpegCore.Enums;

namespace AISportCoach.IntegrationTests.VideoProcessing;

public record OptimizationResult(
    string OutputVideoPath,
    string MetadataJsonPath,
    TimeSpan OriginalDuration,
    TimeSpan OptimizedDuration,
    double CompressionRatio);

public class VideoOptimizer(string outputDirectory)
{
    private const double SilenceNoiseDb = -50;
    private const double SilenceMinSeconds = 2.0;
    private const double FreezeNoiseTolerance = 0.001;
    private const double FreezeMinSeconds = 2.0;
    private const double BufferSeconds = 2.0;

    public async Task<OptimizationResult> OptimizeAsync(
        string inputPath, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var mediaInfo = await FFProbe.AnalyseAsync(inputPath, cancellationToken: ct);
        var totalDuration = mediaInfo.Duration;

        var silentSegments = await DetectSilenceAsync(inputPath, ct);
        var frozenSegments = await DetectFreezeAsync(inputPath, ct);

        var allDead = MergeSegments([.. silentSegments, .. frozenSegments]);
        var deadWithReasons = AssignReasons(allDead, silentSegments, frozenSegments);
        var playSegments = InvertSegments(allDead, totalDuration, BufferSeconds);

        var outputVideoPath = Path.Combine(outputDirectory, "optimized.mp4");
        await TrimAndConcatenateAsync(inputPath, playSegments, outputVideoPath, ct);

        var outputInfo = await FFProbe.AnalyseAsync(outputVideoPath, cancellationToken: ct);
        var optimizedDuration = outputInfo.Duration;
        var compressionRatio = totalDuration.TotalSeconds > 0
            ? optimizedDuration.TotalSeconds / totalDuration.TotalSeconds
            : 1.0;

        var metadataPath = Path.Combine(outputDirectory, "metadata.json");
        await WriteMetadataAsync(metadataPath, inputPath, totalDuration, optimizedDuration,
            compressionRatio, playSegments, deadWithReasons, sw.Elapsed, ct);

        return new OptimizationResult(
            outputVideoPath, metadataPath, totalDuration, optimizedDuration, compressionRatio);
    }

    private static async Task<List<(TimeSpan Start, TimeSpan End)>> DetectSilenceAsync(
        string inputPath, CancellationToken ct)
    {
        var nullOutput = OperatingSystem.IsWindows() ? "NUL" : "/dev/null";
        var lines = new List<string>();

        await FFMpegArguments
            .FromFileInput(inputPath, verifyExists: true)
            .OutputToFile(nullOutput, overwrite: false, addArguments: opts => opts
                .WithCustomArgument(
                    $"-af silencedetect=noise={SilenceNoiseDb}dB:duration={SilenceMinSeconds}")
                .ForceFormat("null"))
            .NotifyOnError(line => lines.Add(line))
            .ProcessAsynchronously();

        return ParseTimeSegments(lines, "silence_start", "silence_end");
    }

    private static async Task<List<(TimeSpan Start, TimeSpan End)>> DetectFreezeAsync(
        string inputPath, CancellationToken ct)
    {
        var nullOutput = OperatingSystem.IsWindows() ? "NUL" : "/dev/null";
        var lines = new List<string>();

        await FFMpegArguments
            .FromFileInput(inputPath, verifyExists: true)
            .OutputToFile(nullOutput, overwrite: false, addArguments: opts => opts
                .WithCustomArgument(
                    $"-vf freezedetect=noise={FreezeNoiseTolerance}:duration={FreezeMinSeconds}")
                .ForceFormat("null"))
            .NotifyOnError(line => lines.Add(line))
            .ProcessAsynchronously();

        return ParseTimeSegments(lines, "freeze_start", "freeze_end");
    }

    private static List<(TimeSpan Start, TimeSpan End)> ParseTimeSegments(
        List<string> lines, string startKey, string endKey)
    {
        var segments = new List<(TimeSpan Start, TimeSpan End)>();
        double? pendingStart = null;

        var startRx = new Regex($@"{Regex.Escape(startKey)}:\s*(\d+\.?\d*)");
        var endRx   = new Regex($@"{Regex.Escape(endKey)}:\s*(\d+\.?\d*)");

        foreach (var line in lines)
        {
            var startMatch = startRx.Match(line);
            if (startMatch.Success)
            {
                pendingStart = double.Parse(startMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                continue;
            }

            var endMatch = endRx.Match(line);
            if (endMatch.Success && pendingStart.HasValue)
            {
                var end = double.Parse(endMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                segments.Add((TimeSpan.FromSeconds(pendingStart.Value), TimeSpan.FromSeconds(end)));
                pendingStart = null;
            }
        }

        return segments;
    }

    internal static List<(TimeSpan Start, TimeSpan End)> MergeSegments(
        List<(TimeSpan Start, TimeSpan End)> segments)
    {
        if (segments.Count == 0) return [];

        var sorted = segments.OrderBy(s => s.Start).ToList();
        var merged = new List<(TimeSpan Start, TimeSpan End)> { sorted[0] };

        foreach (var (start, end) in sorted.Skip(1))
        {
            var last = merged[^1];
            if (start <= last.End)
                merged[^1] = (last.Start, end > last.End ? end : last.End);
            else
                merged.Add((start, end));
        }

        return merged;
    }

    internal static List<(TimeSpan Start, TimeSpan End)> InvertSegments(
        List<(TimeSpan Start, TimeSpan End)> deadSegments,
        TimeSpan totalDuration,
        double bufferSeconds)
    {
        if (deadSegments.Count == 0)
            return [(TimeSpan.Zero, totalDuration)];

        var buffer = TimeSpan.FromSeconds(bufferSeconds);
        var play = new List<(TimeSpan Start, TimeSpan End)>();
        var playStart = TimeSpan.Zero;

        foreach (var (deadStart, deadEnd) in deadSegments)
        {
            // Play ends 'buffer' into the dead zone (keep 2s of context)
            var playEnd = deadStart + buffer;
            if (playEnd > totalDuration) playEnd = totalDuration;
            if (playStart < playEnd)
                play.Add((playStart, playEnd));

            // Next play starts 'buffer' before the dead zone ends
            var candidate = deadEnd - buffer;
            playStart = candidate < TimeSpan.Zero ? TimeSpan.Zero : candidate;
        }

        if (playStart < totalDuration)
            play.Add((playStart, totalDuration));

        // Buffers may cause overlaps — merge them away
        return MergeSegments(play);
    }

    private static List<(TimeSpan Start, TimeSpan End, string Reason)> AssignReasons(
        List<(TimeSpan Start, TimeSpan End)> dead,
        List<(TimeSpan Start, TimeSpan End)> silent,
        List<(TimeSpan Start, TimeSpan End)> frozen)
    {
        return dead.Select(d =>
        {
            var fromSilence = silent.Any(s => s.Start < d.End && s.End > d.Start);
            var fromFreeze  = frozen.Any(f => f.Start < d.End && f.End > d.Start);
            var reason = (fromSilence, fromFreeze) switch
            {
                (true,  true)  => "silence_and_freeze",
                (true,  false) => "silence_only",
                (false, true)  => "freeze_only",
                _              => "unknown"
            };
            return (d.Start, d.End, reason);
        }).ToList();
    }

    private static async Task TrimAndConcatenateAsync(
        string inputPath,
        List<(TimeSpan Start, TimeSpan End)> playSegments,
        string outputPath,
        CancellationToken ct)
    {
        // Single segment: trim directly to final output
        if (playSegments.Count == 1)
        {
            var (start, end) = playSegments[0];
            await FFMpegArguments
                .FromFileInput(inputPath, verifyExists: true, addArguments: opts => opts
                    .Seek(start)
                    .WithDuration(end - start))
                .OutputToFile(outputPath, overwrite: true, addArguments: opts => opts
                    .WithVideoCodec(VideoCodec.LibX264)
                    .WithAudioCodec(AudioCodec.Aac)
                    .WithCustomArgument("-crf 23"))
                .ProcessAsynchronously();
            return;
        }

        // Multiple segments: trim each to temp file, then concat
        var workDir = Path.GetDirectoryName(outputPath)!;
        var segmentPaths = new List<string>();

        try
        {
            for (var i = 0; i < playSegments.Count; i++)
            {
                var (start, end) = playSegments[i];
                var segPath = Path.Combine(workDir, $"seg_{i:D3}.mp4");

                await FFMpegArguments
                    .FromFileInput(inputPath, verifyExists: true, addArguments: opts => opts
                        .Seek(start)
                        .WithDuration(end - start))
                    .OutputToFile(segPath, overwrite: true, addArguments: opts => opts
                        .WithVideoCodec(VideoCodec.LibX264)
                        .WithAudioCodec(AudioCodec.Aac)
                        .WithCustomArgument("-crf 23"))
                    .ProcessAsynchronously();

                segmentPaths.Add(segPath);
            }

            // Write concat list (forward slashes required by FFmpeg concat demuxer)
            var concatList = Path.Combine(workDir, "concat_list.txt");
            await File.WriteAllLinesAsync(concatList,
                segmentPaths.Select(p => $"file '{p.Replace("\\", "/")}'"), ct);

            await FFMpegArguments
                .FromFileInput(concatList, verifyExists: true, addArguments: opts => opts
                    .WithCustomArgument("-f concat -safe 0"))
                .OutputToFile(outputPath, overwrite: true, addArguments: opts => opts
                    .WithCustomArgument("-c copy -movflags +faststart"))
                .ProcessAsynchronously();
        }
        finally
        {
            foreach (var segPath in segmentPaths.Where(File.Exists))
                File.Delete(segPath);
        }
    }

    private static async Task WriteMetadataAsync(
        string metadataPath,
        string inputPath,
        TimeSpan originalDuration,
        TimeSpan optimizedDuration,
        double compressionRatio,
        List<(TimeSpan Start, TimeSpan End)> playSegments,
        List<(TimeSpan Start, TimeSpan End, string Reason)> removedSegments,
        TimeSpan processingTime,
        CancellationToken ct)
    {
        var metadata = new
        {
            source_video           = Path.GetFileName(inputPath),
            source_duration_sec    = Math.Round(originalDuration.TotalSeconds, 3),
            optimized_duration_sec = Math.Round(optimizedDuration.TotalSeconds, 3),
            compression_ratio      = Math.Round(compressionRatio, 4),
            segments = playSegments.Select((s, i) => new
            {
                id             = i + 1,
                type           = "play",
                original_start = s.Start.ToString(@"hh\:mm\:ss\.fff"),
                original_end   = s.End.ToString(@"hh\:mm\:ss\.fff"),
                signals        = new[] { "motion", "audio" }
            }).ToArray(),
            removed_segments = removedSegments.Select(r => new
            {
                type           = "dead_time",
                original_start = r.Start.ToString(@"hh\:mm\:ss\.fff"),
                original_end   = r.End.ToString(@"hh\:mm\:ss\.fff"),
                reason         = r.Reason
            }).ToArray(),
            processing_info = new
            {
                pipeline_version    = "1.0-mvp",
                processing_time_sec = Math.Round(processingTime.TotalSeconds, 2),
                detector            = "ffmpeg-silencedetect-freezedetect"
            }
        };

        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(metadataPath, json, ct);
    }
}
