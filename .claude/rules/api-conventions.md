# API Conventions

## Controllers

Every controller must have:
```csharp
[ApiController]
[Route("api/v{version:apiVersion}/resource")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Tags("ResourceName")]
```

- Inherit `ControllerBase` — never `Controller`
- Actions dispatch to MediatR and return — zero business logic in controllers
- Every action needs `[EndpointSummary]`, `[EndpointDescription]`, and all `[ProducesResponseType]` attributes
- See canonical example: `src/AISportCoach.API/Controllers/VideosController.cs`

## DTOs

- Use `record` types — all DTOs live in `AISportCoach.API/DTOs/`
- Map domain → DTO in `Mappers/` via extension methods: `entity.ToDto()`
- Never map inside controllers or handlers

## Validation

- Use `[FromQuery]`, `[FromForm]`, `[FromBody]` explicitly on every parameter
- Validation attributes on query/DTO records (`[Range]`, `[Required]`, etc.)
- `[ApiController]` returns `422 UnprocessableEntity` automatically for model errors — no manual checks needed

## Route Names

- One static class per controller in `AISportCoach.API/RouteNames/<Controller>RouteNames.cs`
- Namespace: `AISportCoach.API.RouteNames`
- Use the constant in both the attribute and `CreatedAtRoute` — never hardcode strings

```csharp
// RouteNames/ReportRouteNames.cs
public static class ReportRouteNames
{
    public const string GetReport = "GetReport";
}

// In controller
[HttpGet("{id:guid}", Name = ReportRouteNames.GetReport)]

// In another controller
return CreatedAtRoute(ReportRouteNames.GetReport, new { reportId = dto.Id }, dto);
```

## Patterns NOT Used Here

- No Minimal APIs — use `ControllerBase`
- No AutoMapper — write explicit `ToDto()` extension methods
- No FluentValidation — use data annotation attributes + `[ApiController]`
- No global `try/catch` in controllers — `ExceptionHandlingMiddleware` handles it
