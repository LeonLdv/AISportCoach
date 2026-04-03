#pragma warning disable SKEXP0070, SKEXP0110

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AISportCoach.Application.Plugins;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;

namespace AISportCoach.UnitTests.Integration;

/// <summary>
/// Integration test: 2-agent Semantic Kernel pipeline that analyzes a tennis video.
///
/// Agent 1 — VideoAnalysisAgent:
///   Has VideoAnalysisPlugin. Receives a Gemini file URI, calls AnalyzeVideo, and
///   returns a JSON array of technique observations.
///
/// Agent 2 — ReportGenerationAgent:
///   Has ReportGenerationPlugin. Receives the observations JSON from Agent 1, calls
///   GenerateCoachingReport, and returns a structured coaching report JSON.
///
/// The video file URI is cached locally (shared with GeminiVideoAnalysisTest) to
/// avoid redundant uploads across test runs.
/// </summary>
public class TennisCoachAgentPipelineTest
{
    private const string VideoPath =
        @"C:\projects\AISportCoach\uploads\6f095ecb-0e07-4ddb-8cfb-4fecb126ed17_part1(split-video.com) (1).mp4";

    private const string Model = "gemini-2.5-flash";
    private const string PlayerLevel = "Intermediate";

    // Shared with GeminiVideoAnalysisTest to avoid re-uploading on every run
    private static readonly string CacheFile =
        Path.Combine(Path.GetTempPath(), "gemini_file_uri_cache.json");

    // ── Helpers ───────────────────────────────────────────────────────────────

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

    private static string? LoadCachedUri()
    {
        if (!File.Exists(CacheFile)) return null;
        using var doc = JsonDocument.Parse(File.ReadAllText(CacheFile));
        return doc.RootElement.TryGetProperty(VideoPath, out var val) ? val.GetString() : null;
    }

    private static void SaveCachedUri(string fileUri)
    {
        var cache = File.Exists(CacheFile)
            ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(CacheFile)) ?? []
            : new Dictionary<string, string>();
        cache[VideoPath] = fileUri;
        File.WriteAllText(CacheFile, JsonSerializer.Serialize(cache));
    }

    private static async Task<bool> IsFileActiveAsync(HttpClient http, string apiKey, string fileUri)
    {
        try
        {
            var fileName = fileUri.Split('/').TakeLast(2).Aggregate((a, b) => $"{a}/{b}");
            var response = await http.GetAsync(
                $"https://generativelanguage.googleapis.com/v1beta/{fileName}?key={apiKey}");
            if (!response.IsSuccessStatusCode) return false;

            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("state").GetString() == "ACTIVE";
        }
        catch { return false; }
    }

    private static async Task<string> UploadVideoAsync(HttpClient http, string apiKey, string videoPath)
    {
        var fileBytes = await File.ReadAllBytesAsync(videoPath);
        var displayName = Path.GetFileName(videoPath);

        var initiateRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/upload/v1beta/files?uploadType=resumable&key={apiKey}");

        var metadata = JsonSerializer.Serialize(new { file = new { display_name = displayName } });
        initiateRequest.Content = new StringContent(metadata, Encoding.UTF8, "application/json");
        initiateRequest.Headers.Add("X-Goog-Upload-Protocol", "resumable");
        initiateRequest.Headers.Add("X-Goog-Upload-Command", "start");
        initiateRequest.Headers.Add("X-Goog-Upload-Header-Content-Length", fileBytes.Length.ToString());
        initiateRequest.Headers.Add("X-Goog-Upload-Header-Content-Type", "video/mp4");

        var initiateResponse = await http.SendAsync(initiateRequest);
        initiateResponse.EnsureSuccessStatusCode();
        var uploadUrl = initiateResponse.Headers.GetValues("X-Goog-Upload-URL").First();

        var uploadRequest = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
        uploadRequest.Content = new ByteArrayContent(fileBytes);
        uploadRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        uploadRequest.Headers.Add("X-Goog-Upload-Offset", "0");
        uploadRequest.Headers.Add("X-Goog-Upload-Command", "upload, finalize");

        var uploadResponse = await http.SendAsync(uploadRequest);
        uploadResponse.EnsureSuccessStatusCode();

        var result = await uploadResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(result);
        return doc.RootElement.GetProperty("file").GetProperty("uri").GetString()!;
    }

    private static async Task WaitForFileActiveAsync(HttpClient http, string apiKey, string fileUri)
    {
        var fileName = fileUri.Split('/').TakeLast(2).Aggregate((a, b) => $"{a}/{b}");
        for (var i = 0; i < 20; i++)
        {
            var body = await http.GetStringAsync(
                $"https://generativelanguage.googleapis.com/v1beta/{fileName}?key={apiKey}");
            using var doc = JsonDocument.Parse(body);
            var state = doc.RootElement.GetProperty("state").GetString();
            if (state == "ACTIVE") return;
            if (state == "FAILED") throw new Exception("Gemini file processing failed.");
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
        throw new TimeoutException("Gemini file did not become ACTIVE within timeout.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<string> RunAgentAsync(ChatCompletionAgent agent, string prompt)
    {
        var thread = new ChatHistoryAgentThread();
        string result = "";
        await foreach (var msg in agent.InvokeAsync(prompt, thread))
            result = msg.Message.Content ?? "";
        return result;
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TwoAgentPipeline_AnalyzesVideo_AndGeneratesCoachingReport()
    {
        Assert.True(File.Exists(VideoPath), $"Video not found: {VideoPath}");

        var apiKey = ReadApiKey();
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };

        // ── Resolve Gemini file URI (reuse cache from previous test runs) ──────
        var fileUri = LoadCachedUri();
        if (fileUri is not null && !await IsFileActiveAsync(http, apiKey, fileUri))
        {
            fileUri = null;
            SaveCachedUri(null!);
        }

        if (fileUri is null)
        {
            fileUri = await UploadVideoAsync(http, apiKey, VideoPath);
            SaveCachedUri(fileUri);
            await WaitForFileActiveAsync(http, apiKey, fileUri);
        }

        Assert.NotNull(fileUri);

        // ── Agent 1: VideoAnalysisAgent ───────────────────────────────────────
        // Receives the file URI, invokes VideoAnalysisPlugin.AnalyzeVideo, returns observations JSON.
        var analysisKernel = Kernel.CreateBuilder()
            .AddGoogleAIGeminiChatCompletion(Model, apiKey)
            .Build();
        analysisKernel.Plugins.AddFromObject(
            new VideoAnalysisPlugin(NullLogger<VideoAnalysisPlugin>.Instance),
            "VideoAnalysis");

        var videoAnalysisAgent = new ChatCompletionAgent
        {
            Name = "VideoAnalysisAgent",
            Instructions = $"""
                You are an expert tennis technique analyst.
                When given a Gemini video file URI, call the AnalyzeVideo tool to analyze the video.
                Return only the raw JSON observations array — no additional commentary.
                Player level: {PlayerLevel}.
                """,
            Kernel = analysisKernel,
            Arguments = new KernelArguments(new PromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            })
        };

        var analysisThread = new ChatHistoryAgentThread();
        string observationsJson = "";

        await foreach (var msg in videoAnalysisAgent.InvokeAsync(
            $"Analyze this tennis video and return technique observations JSON. File URI: {fileUri}",
            analysisThread))
        {
            observationsJson = msg.Message.Content ?? "";
        }

        Assert.NotEmpty(observationsJson);

        // ── Agent 2: ReportGenerationAgent ────────────────────────────────────
        // Receives observations JSON from Agent 1, invokes GenerateCoachingReport, returns report JSON.
        var reportKernel = Kernel.CreateBuilder()
            .AddGoogleAIGeminiChatCompletion(Model, apiKey)
            .Build();
        reportKernel.Plugins.AddFromObject(
            new ReportGenerationPlugin(NullLogger<ReportGenerationPlugin>.Instance),
            "ReportGeneration");

        var reportGenerationAgent = new ChatCompletionAgent
        {
            Name = "ReportGenerationAgent",
            Instructions = $"""
                You are a professional tennis coach report writer.
                When given technique observations JSON, call the GenerateCoachingReport tool to produce
                a structured coaching report. Return only the JSON — no additional commentary.
                Player level: {PlayerLevel}.
                """,
            Kernel = reportKernel,
            Arguments = new KernelArguments(new PromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            })
        };

        var reportThread = new ChatHistoryAgentThread();
        string reportJson = "";

        await foreach (var msg in reportGenerationAgent.InvokeAsync(
            $"Generate a coaching report from these technique observations (player level: {PlayerLevel}):\n{observationsJson}",
            reportThread))
        {
            reportJson = msg.Message.Content ?? "";
        }

        Assert.NotEmpty(reportJson);

        // ── Validate report structure ─────────────────────────────────────────
        // Strip markdown fences if the model wrapped the JSON
        var jsonStart = reportJson.IndexOf('{');
        var jsonEnd = reportJson.LastIndexOf('}');
        if (jsonStart >= 0 && jsonEnd > jsonStart)
            reportJson = reportJson[jsonStart..(jsonEnd + 1)];

        using var reportDoc = JsonDocument.Parse(reportJson);
        var root = reportDoc.RootElement;

        Assert.True(root.TryGetProperty("overallScore", out var scoreEl),
            "Report must contain 'overallScore'");
        Assert.InRange(scoreEl.GetInt32(), 0, 100);

        Assert.True(root.TryGetProperty("executiveSummary", out var summaryEl),
            "Report must contain 'executiveSummary'");
        Assert.NotEmpty(summaryEl.GetString() ?? "");

        Assert.True(root.TryGetProperty("observations", out var observationsEl),
            "Report must contain 'observations'");
        Assert.True(observationsEl.GetArrayLength() > 0,
            "At least one observation expected");

        Assert.True(root.TryGetProperty("recommendations", out var recommendationsEl),
            "Report must contain 'recommendations'");
        Assert.True(recommendationsEl.GetArrayLength() > 0,
            "At least one recommendation expected");
    }

    [Fact]
    public async Task ThreeAgentPipeline_AnalyzesVideo_WithParallelRatingAndReport()
    {
        Assert.True(File.Exists(VideoPath), $"Video not found: {VideoPath}");

        var apiKey = ReadApiKey();
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };

        // ── Resolve Gemini file URI (reuse cache) ─────────────────────────────
        var fileUri = LoadCachedUri();
        if (fileUri is not null && !await IsFileActiveAsync(http, apiKey, fileUri))
        {
            fileUri = null;
            SaveCachedUri(null!);
        }

        if (fileUri is null)
        {
            fileUri = await UploadVideoAsync(http, apiKey, VideoPath);
            SaveCachedUri(fileUri);
            await WaitForFileActiveAsync(http, apiKey, fileUri);
        }

        Assert.NotNull(fileUri);

        // ── Agent 1: VideoAnalysisAgent ───────────────────────────────────────
        var analysisKernel = Kernel.CreateBuilder()
            .AddGoogleAIGeminiChatCompletion(Model, apiKey)
            .Build();
        analysisKernel.Plugins.AddFromObject(
            new VideoAnalysisPlugin(NullLogger<VideoAnalysisPlugin>.Instance),
            "VideoAnalysis");

        var videoAnalysisAgent = new ChatCompletionAgent
        {
            Name = "VideoAnalysisAgent",
            Instructions = $"""
                You are an expert tennis technique analyst.
                When given a Gemini video file URI, call the AnalyzeVideo tool to analyze the video.
                Return only the raw JSON observations array — no additional commentary.
                Player level: {PlayerLevel}.
                """,
            Kernel = analysisKernel,
            Arguments = new KernelArguments(new PromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            })
        };

        var observationsJson = await RunAgentAsync(
            videoAnalysisAgent,
            $"Analyze this tennis video and return technique observations JSON. File URI: {fileUri}");

        Assert.NotEmpty(observationsJson);

        // ── Agents 2 & 3: ReportGenerationAgent + NTRPRatingAgent (parallel) ──
        var reportKernel = Kernel.CreateBuilder()
            .AddGoogleAIGeminiChatCompletion(Model, apiKey)
            .Build();
        reportKernel.Plugins.AddFromObject(
            new ReportGenerationPlugin(NullLogger<ReportGenerationPlugin>.Instance),
            "ReportGeneration");

        var reportGenerationAgent = new ChatCompletionAgent
        {
            Name = "ReportGenerationAgent",
            Instructions = $"""
                You are a professional tennis coach report writer.
                When given technique observations JSON, call the GenerateCoachingReport tool to produce
                a structured coaching report. Return only the JSON — no additional commentary.
                Player level: {PlayerLevel}.
                """,
            Kernel = reportKernel,
            Arguments = new KernelArguments(new PromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            })
        };

        var ntrpKernel = Kernel.CreateBuilder()
            .AddGoogleAIGeminiChatCompletion(Model, apiKey)
            .Build();
        ntrpKernel.Plugins.AddFromObject(
            new NtrpRatingPlugin(NullLogger<NtrpRatingPlugin>.Instance),
            "NtrpRating");

        var ntrpRatingAgent = new ChatCompletionAgent
        {
            Name = "NTRPRatingAgent",
            Instructions = """
                You are a certified USTA tennis rater. Given technique observations,
                call DetermineNtrpRating to assign an evidence-based NTRP rating.
                Return only the JSON result — no commentary.
                """,
            Kernel = ntrpKernel,
            Arguments = new KernelArguments(new PromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            })
        };

        var reportTask = RunAgentAsync(
            reportGenerationAgent,
            $"Generate a coaching report from these technique observations (player level: {PlayerLevel}):\n{observationsJson}");
        var ntrpTask = RunAgentAsync(
            ntrpRatingAgent,
            $"Determine NTRP rating for: {observationsJson}");

        var results = await Task.WhenAll(reportTask, ntrpTask);

        var reportJson = results[0];
        var ntrpJson   = results[1];

        Assert.NotEmpty(reportJson);
        Assert.NotEmpty(ntrpJson);

        // ── Validate report structure ─────────────────────────────────────────
        var reportStart = reportJson.IndexOf('{');
        var reportEnd   = reportJson.LastIndexOf('}');
        if (reportStart >= 0 && reportEnd > reportStart)
            reportJson = reportJson[reportStart..(reportEnd + 1)];

        using var reportDoc = JsonDocument.Parse(reportJson);
        var reportRoot = reportDoc.RootElement;

        Assert.True(reportRoot.TryGetProperty("overallScore", out var scoreEl),
            "Report must contain 'overallScore'");
        Assert.InRange(scoreEl.GetInt32(), 0, 100);

        Assert.True(reportRoot.TryGetProperty("executiveSummary", out var summaryEl),
            "Report must contain 'executiveSummary'");
        Assert.NotEmpty(summaryEl.GetString() ?? "");

        Assert.True(reportRoot.TryGetProperty("observations", out var obsEl),
            "Report must contain 'observations'");
        Assert.True(obsEl.GetArrayLength() > 0, "At least one observation expected");

        Assert.True(reportRoot.TryGetProperty("recommendations", out var recsEl),
            "Report must contain 'recommendations'");
        Assert.True(recsEl.GetArrayLength() > 0, "At least one recommendation expected");

        // ── Validate NTRP result ──────────────────────────────────────────────
        var ntrpStart = ntrpJson.IndexOf('{');
        var ntrpEnd   = ntrpJson.LastIndexOf('}');
        if (ntrpStart >= 0 && ntrpEnd > ntrpStart)
            ntrpJson = ntrpJson[ntrpStart..(ntrpEnd + 1)];

        using var ntrpDoc = JsonDocument.Parse(ntrpJson);
        var ntrpRoot = ntrpDoc.RootElement;

        Assert.True(ntrpRoot.TryGetProperty("ntrpRating", out var ratingEl),
            "NTRP result must contain 'ntrpRating'");
        var rating = ratingEl.GetDouble();
        Assert.InRange(rating, 1.5, 7.0);

        Assert.True(ntrpRoot.TryGetProperty("ratingJustification", out _),
            "NTRP result must contain 'ratingJustification'");

        Assert.True(ntrpRoot.TryGetProperty("evidence", out var evidenceEl),
            "NTRP result must contain 'evidence'");
        Assert.True(evidenceEl.GetArrayLength() > 0, "At least one evidence item expected");

        foreach (var item in evidenceEl.EnumerateArray())
        {
            Assert.True(item.TryGetProperty("observation", out _));
            Assert.True(item.TryGetProperty("ntrpIndicator", out _));
            Assert.True(item.TryGetProperty("supportedLevel", out _));
        }
    }
}
