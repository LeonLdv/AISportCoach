using AISportCoach.API.DTOs;
using AISportCoach.API.RouteNames;
using AISportCoach.Application.UseCases.AskCoach;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AISportCoach.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/coach")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Tags("Coach")]
public class CoachController(IMediator mediator) : ControllerBase
{
    [HttpPost("ask", Name = CoachRouteNames.AskCoach)]
    [EndpointSummary("Ask the AI coach a question")]
    [EndpointDescription("Answers a natural-language coaching question grounded in the player's session history.")]
    [ProducesResponseType(typeof(CoachAnswerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CoachAnswerDto>> Ask(
        [FromBody] CoachAskRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CoachAskQuery(request.Question), cancellationToken);
        return Ok(new CoachAnswerDto(result.Answer, result.Advice, result.Drills));
    }
}
