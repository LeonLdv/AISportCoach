using System.Text.Json;

namespace AISportCoach.IntegrationTests.VideoProcessing;

public class VideoOptimizerTest : IAsyncDisposable
{
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(), $"VideoOptimizerTest_{Guid.NewGuid():N}");

    public VideoOptimizerTest() => Directory.CreateDirectory(_tempDir);

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
        return ValueTask.CompletedTask;
    }

    private static string FindTestVideo(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !dir.GetFiles("AISportCoach.slnx").Any() && !dir.GetFiles("AISportCoach.sln").Any())
            dir = dir.Parent;

        if (dir == null)
            throw new InvalidOperationException("Could not find solution root (AISportCoach.slnx not found).");

        var path = Path.Combine(
            dir.FullName, "tests", "AISportCoach.FunctionalTests", "TestData", fileName);

        if (!File.Exists(path))
            throw new FileNotFoundException($"Test video not found: {path}");

        return path;
    }

    [Fact]
    public void MergeSegments_OverlappingAndAdjacent_MergesCorrectly()
    {
        var input = new List<(TimeSpan, TimeSpan)>
        {
            (TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20)),
            (TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(25)), // overlaps previous
            (TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(40))  // separate
        };

        var merged = VideoOptimizer.MergeSegments(input);

        Assert.Equal(2, merged.Count);
        Assert.Equal(TimeSpan.FromSeconds(10), merged[0].Start);
        Assert.Equal(TimeSpan.FromSeconds(25), merged[0].End);
        Assert.Equal(TimeSpan.FromSeconds(30), merged[1].Start);
        Assert.Equal(TimeSpan.FromSeconds(40), merged[1].End);
    }

    [Fact]
    public void InvertSegments_TwoDeadZones_ProducesThreePlaySegmentsWithBuffers()
    {
        var dead = new List<(TimeSpan, TimeSpan)>
        {
            (TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20)),
            (TimeSpan.FromSeconds(35), TimeSpan.FromSeconds(50))
        };
        var total = TimeSpan.FromSeconds(60);

        var play = VideoOptimizer.InvertSegments(dead, total, bufferSeconds: 2.0);

        // Expected: [0, 12], [18, 37], [48, 60]
        Assert.Equal(3, play.Count);
        Assert.Equal(TimeSpan.Zero,              play[0].Start);
        Assert.Equal(TimeSpan.FromSeconds(12),   play[0].End);
        Assert.Equal(TimeSpan.FromSeconds(18),   play[1].Start);
        Assert.Equal(TimeSpan.FromSeconds(37),   play[1].End);
        Assert.Equal(TimeSpan.FromSeconds(48),   play[2].Start);
        Assert.Equal(TimeSpan.FromSeconds(60),   play[2].End);
    }

    [Fact]
    public void InvertSegments_NoDeadZones_ReturnsEntireVideo()
    {
        var total = TimeSpan.FromSeconds(30);
        var play = VideoOptimizer.InvertSegments([], total, bufferSeconds: 2.0);

        Assert.Single(play);
        Assert.Equal(TimeSpan.Zero, play[0].Start);
        Assert.Equal(total,         play[0].End);
    }

    [Theory]
    [InlineData("Serve-Leo.mp4")]
  //  [InlineData("sample.mp4")]
    public async Task OptimizeAsync_RealVideo_ProducesValidOutput(string fileName)
    {
        var inputPath = FindTestVideo(fileName);
        var outputDir = Path.Combine(_tempDir, Path.GetFileNameWithoutExtension(fileName));
        Directory.CreateDirectory(outputDir);

        var optimizer = new VideoOptimizer(outputDir);
        var result = await optimizer.OptimizeAsync(inputPath);

        // Output files exist
        Assert.True(File.Exists(result.OutputVideoPath),
            $"Output video must exist: {result.OutputVideoPath}");
        Assert.True(File.Exists(result.MetadataJsonPath),
            $"Metadata JSON must exist: {result.MetadataJsonPath}");

        // Durations are sane
        Assert.True(result.OriginalDuration > TimeSpan.Zero,
            "Original duration must be positive");
        Assert.True(result.OptimizedDuration > TimeSpan.Zero,
            "Optimized duration must be positive");
        // Allow 1 s tolerance: re-encoding can shift duration slightly due to frame boundary alignment
        Assert.True(result.OptimizedDuration <= result.OriginalDuration + TimeSpan.FromSeconds(1),
            $"Optimized {result.OptimizedDuration} must be ≤ original {result.OriginalDuration} (+ 1s tolerance)");

        // Compression ratio matches measured durations (within 1% tolerance)
        var expectedRatio = result.OptimizedDuration.TotalSeconds / result.OriginalDuration.TotalSeconds;
        Assert.Equal(expectedRatio, result.CompressionRatio, precision: 2);

        // Compression ratio in valid range (allow slightly above 1.0 for re-encoding frame-boundary drift)
        Assert.InRange(result.CompressionRatio, 0.0, 1.01);

        // Metadata JSON is valid and has all required fields
        var json = await File.ReadAllTextAsync(result.MetadataJsonPath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("source_video", out _),
            "source_video field required");
        Assert.True(root.TryGetProperty("source_duration_sec", out _),
            "source_duration_sec required");
        Assert.True(root.TryGetProperty("optimized_duration_sec", out _),
            "optimized_duration_sec required");
        Assert.True(root.TryGetProperty("compression_ratio", out _),
            "compression_ratio required");
        Assert.True(root.TryGetProperty("segments", out var segments),
            "segments array required");
        Assert.True(root.TryGetProperty("processing_info", out var procInfo),
            "processing_info required");

        Assert.Equal(JsonValueKind.Array, segments.ValueKind);
        Assert.True(segments.GetArrayLength() > 0,
            "At least one play segment expected");

        Assert.Equal("ffmpeg-silencedetect-freezedetect",
            procInfo.GetProperty("detector").GetString());
    }
}
