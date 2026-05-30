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
        var videoUpload = CreateVideoUpload(Guid.NewGuid(), report.VideoUploadId);
        SetNavigation(report, "VideoUpload", videoUpload);
        return report;
    }

    private static VideoUpload CreateVideoUpload(Guid userId, Guid videoId)
    {
        var upload = (VideoUpload)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(VideoUpload));
        SetProperty(upload, "Id", videoId);
        SetProperty(upload, "UserId", userId);
        return upload;
    }

    private static void SetNavigation<T, TNav>(T obj, string propName, TNav value)
        where T : class where TNav : class
        // null here means a test-setup mistake (wrong property name) — let it NRE loudly
        => typeof(T).GetProperty(propName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(obj, value);

    private static void SetProperty<T>(object obj, string propName, T value)
        // null here means a test-setup mistake (wrong property name) — let it NRE loudly
        => obj.GetType().GetProperty(propName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(obj, value);

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
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

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
        // 1 summary + 1 observation = 2 chunks
        const int expectedChunkCount = 2;

        var channel = Channel.CreateUnbounded<Guid>();
        channel.Writer.TryWrite(report.Id);
        channel.Writer.Complete();

        var mockReportRepo = new Mock<ICoachingReportRepository>();
        mockReportRepo.Setup(r => r.GetWithDetailsAsync(report.Id, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(report);

        var mockEmbeddingService = new Mock<IEmbeddingService>();
        mockEmbeddingService
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), EmbeddingTaskType.Document, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[768]);

        var mockEmbeddingRepo = new Mock<IReportEmbeddingRepository>();

        var sut = BuildService(channel.Reader, mockReportRepo.Object, mockEmbeddingService.Object, mockEmbeddingRepo.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await sut.StartAsync(cts.Token);
        await sut.StopAsync(CancellationToken.None);

        mockEmbeddingRepo.Verify(r => r.AddChunksAsync(
            It.IsAny<Guid>(),
            It.Is<IReadOnlyList<(ReportChunk, float[])>>(l => l.Count == expectedChunkCount),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ReportNotFound_SkipsWithoutCallingAddChunksAsync()
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
        await sut.StopAsync(CancellationToken.None);

        mockEmbeddingRepo.Verify(r => r.AddChunksAsync(
            It.IsAny<Guid>(), It.IsAny<IReadOnlyList<(ReportChunk, float[])>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_EmbeddingServiceThrows_SwallowsExceptionAndDoesNotCallAddChunksAsync()
    {
        var report = BuildReport();

        var channel = Channel.CreateUnbounded<Guid>();
        channel.Writer.TryWrite(report.Id);
        channel.Writer.Complete();

        var mockReportRepo = new Mock<ICoachingReportRepository>();
        mockReportRepo.Setup(r => r.GetWithDetailsAsync(report.Id, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(report);

        var mockEmbeddingService = new Mock<IEmbeddingService>();
        mockEmbeddingService
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<EmbeddingTaskType>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Gemini API unavailable"));

        var mockEmbeddingRepo = new Mock<IReportEmbeddingRepository>();

        var sut = BuildService(channel.Reader, mockReportRepo.Object, mockEmbeddingService.Object, mockEmbeddingRepo.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await sut.StartAsync(cts.Token);
        await sut.StopAsync(CancellationToken.None);

        mockEmbeddingRepo.Verify(r => r.AddChunksAsync(
            It.IsAny<Guid>(), It.IsAny<IReadOnlyList<(ReportChunk, float[])>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
