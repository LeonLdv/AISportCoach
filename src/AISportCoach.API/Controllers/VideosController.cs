using AISportCoach.API.DTOs;
using AISportCoach.API.Mappers;
using AISportCoach.Application.UseCases.AnalyzeNow;
using AISportCoach.Application.UseCases.GetVideo;
using AISportCoach.Application.UseCases.UploadVideo;
using Asp.Versioning;
using AISportCoach.API.RouteNames;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;

namespace AISportCoach.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/videos")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Tags("Videos")]
[Authorize]
public class VideosController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [EndpointSummary("Upload a tennis video")]
    [EndpointDescription("Uploads a tennis video for AI analysis. Returns the created video resource with its ID.")]
    [ProducesResponseType(typeof(VideoResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    // EXPERIMENTAL: per-action size and timeout overrides for large video uploads
    [RequestSizeLimit(1_073_741_824)] // 1 GB — attribute requires a compile-time constant
    [RequestTimeout("VideoUpload")]
    public async Task<ActionResult<VideoResponseDto>> Upload(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (file.Length == 0)
            return ValidationProblem(new ValidationProblemDetails(
                new Dictionary<string, string[]> { ["file"] = ["File must not be empty."] }));

        var result = await mediator.Send(
            new UploadVideoCommand(file.OpenReadStream(), file.FileName, file.Length),
            cancellationToken);

        var dto = new VideoResponseDto(result.Id, result.OriginalFileName, result.FileSizeBytes,
            result.Status, result.CreatedAt);

        return CreatedAtRoute(VideoRouteNames.GetById, new { id = result.Id }, dto);
    }

    [HttpGet("{id:guid}", Name = VideoRouteNames.GetById)]
    [EndpointSummary("Get video metadata and status")]
    [EndpointDescription("Returns metadata and current processing status for the specified video.")]
    [ProducesResponseType(typeof(VideoResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VideoResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var video = await mediator.Send(new GetVideoQuery(id), cancellationToken);
        return Ok(new VideoResponseDto(video.Id, video.OriginalFileName, video.FileSizeBytes,
            video.Status.ToString(), video.CreatedAt));
    }

    [HttpPost("{videoId:guid}/analyze")]
    [EndpointSummary("Analyze an uploaded video")]
    [EndpointDescription("Optionally pass { \"scopes\": [\"Forehand\", \"Ntrp\"] } to limit analysis. Omit or send empty body to analyze all scopes including NTRP.")]
    [ProducesResponseType(typeof(CoachingReportResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CoachingReportResponseDto>> Analyze(
        Guid videoId,
        [FromBody] AnalyzeVideoRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        var scopes = request?.Scopes;
        var command = scopes is null or { Length: 0 }
            ? AnalyzeNowCommand.ForAllScopes(videoId)
            : new AnalyzeNowCommand(videoId, scopes.ToHashSet());

        var report = await mediator.Send(command, cancellationToken);
        var dto = report.ToDto();
        return CreatedAtRoute(ReportRouteNames.GetReport, new { reportId = dto.Id }, dto);
    }
}
