using AISportCoach.API.DTOs;
using AISportCoach.API.Mappers;
using AISportCoach.Application.DTOs;
using AISportCoach.Application.Models;
using AISportCoach.Application.UseCases.GetReport;
using AISportCoach.Application.UseCases.GetReports;
using AISportCoach.Application.UseCases.GetReportsSummary;
using Asp.Versioning;
using AISportCoach.API.RouteNames;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AISportCoach.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/reports")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Tags("Reports")]
[Authorize]
public class ReportsController(IMediator mediator) : ControllerBase
{
    [HttpGet("{reportId:guid}", Name = ReportRouteNames.GetReport)]
    [EndpointSummary("Get a coaching report by ID")]
    [EndpointDescription("Returns the full AI-generated coaching report including NTRP rating, technique observations, and recommendations.")]
    [ProducesResponseType(typeof(CoachingReportResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CoachingReportResponseDto>> GetReport(
        Guid reportId, CancellationToken cancellationToken)
    {
        var report = await mediator.Send(new GetReportQuery(reportId), cancellationToken);
        return Ok(report.ToDto());
    }

    [HttpGet("summary")]
    [EndpointSummary("List coaching report summaries")]
    [EndpointDescription("Returns a paginated list coaching report summaries.")]
    [ProducesResponseType(typeof(PagedResult<CoachingReportSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PagedResult<CoachingReportSummary>>> GetReportsSummary(
        [FromQuery] PaginationQuery pagination, CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetReportsSummaryQuery(pagination.Page, pagination.PageSize), cancellationToken);
        return Ok(result);
    }

    [HttpGet]
    [EndpointSummary("List all coaching reports")]
    [EndpointDescription("Returns a paginated list of coaching reports.")]
    [ProducesResponseType(typeof(PagedResult<CoachingReportResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PagedResult<CoachingReportResponseDto>>> GetReports(
        [FromQuery] PaginationQuery pagination, CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetReportsQuery(pagination.Page, pagination.PageSize), cancellationToken);
        var dtoItems = result.Items.Select(r => r.ToDto()).ToList();
        return Ok(new PagedResult<CoachingReportResponseDto>(dtoItems, result.TotalCount, result.Page, result.PageSize));
    }
}
