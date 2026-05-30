using AISportCoach.Application.Interfaces;
using AISportCoach.Application.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace AISportCoach.Infrastructure.BackgroundServices;

public class ReportEmbeddingBackgroundService(
    ChannelReader<Guid> channelReader,
    IServiceScopeFactory scopeFactory,
    IReportChunker chunker,
    ILogger<ReportEmbeddingBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var reportId in channelReader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var reportRepository = scope.ServiceProvider.GetRequiredService<ICoachingReportRepository>();
                var embeddingService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
                var embeddingRepository = scope.ServiceProvider.GetRequiredService<IReportEmbeddingRepository>();

                var report = await reportRepository.GetWithDetailsAsync(reportId, stoppingToken);
                if (report is null)
                {
                    logger.LogWarning("Report {ReportId} not found for embedding — skipping", reportId);
                    continue;
                }

                var userId = report.VideoUpload.UserId;
                var chunks = chunker.Chunk(report);
                var pairs = new List<(ReportChunk, float[])>(chunks.Count);

                foreach (var chunk in chunks)
                {
                    var embedding = await embeddingService
                        .GenerateEmbeddingAsync(chunk.Text, EmbeddingTaskType.Document, stoppingToken);
                    pairs.Add((chunk, embedding));
                }

                await embeddingRepository.AddChunksAsync(userId, pairs, stoppingToken);
                logger.LogInformation(
                    "[Embedding] Saved {ChunkCount} chunks for report {ReportId}", pairs.Count, reportId);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Embedding] Pipeline failed for report {ReportId}", reportId);
                // swallowed — video stays Processed
            }
        }
    }
}
