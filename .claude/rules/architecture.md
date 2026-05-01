# Architecture Rules

## Layer Boundaries — enforce strictly

- **Domain** — zero NuGet deps beyond BCL. No exceptions.
- **Application** — depends on Domain only. Defines interfaces; never imports Infrastructure types.
- **Infrastructure** — implements Application interfaces. Never references API.
- **API** — references Application for use-case types; wires Infrastructure only in `Program.cs`.

## CQRS via MediatR

- Every mutation → `IRequest<T>` Command (`*Command.cs` + `*Handler.cs`)
- Every read → `IRequest<T>` Query (`*Query.cs` + `*Handler.cs`)
- One handler per command/query. Handlers never call other handlers.
- Folder: `Application/UseCases/{FeatureName}/`
- See canonical example: `src/AISportCoach.Application/UseCases/AnalyzeNow/`

## Repository Pattern

- Interfaces defined in Application (`IVideoRepository`, `ICoachingReportRepository`)
- Implemented in Infrastructure — never bypass via direct EF calls in handlers
- All repository methods take `CancellationToken` as last parameter
- `SaveChangesAsync` is called only inside repository implementations

## C# Conventions

**Use:**
- Primary constructors for DI injection (all existing classes use them — stay consistent)
- `record` types for DTOs, commands, queries, value objects
- File-scoped namespaces (`namespace Foo.Bar;`)
- `var` when type is obvious from the right-hand side
- Collection expressions (`[item1, item2]`)
- Pattern matching and switch expressions over if-else chains
- `async Task<T>` for all I/O — no `.Result`, `.Wait()`, or `async void`
- `CancellationToken` everywhere; never pass `CancellationToken.None` in production code
- Structured logging placeholders (`{VideoId}`) — not string interpolation

**Never use:**
- `!` null-forgiving suppressors without an explanatory comment
- Exceptions for control flow — throw typed `DomainException` subclasses for domain errors
- Comments that explain *what* code does — only *why* when non-obvious

**Naming:**
- Interfaces: `IFooService` (not `FooInterface`)
- Async methods: suffix `Async`
- Constants: `PascalCase` (not `SCREAMING_SNAKE`)
- Private fields: `_camelCase`
- Never use single-letter or abbreviated variable names — use descriptive names that convey intent (e.g., `apiKey` not `k`, `response` not `r`)

**Enums:**
- Always start numbering from `1`, not `0` (e.g., `Free = 1, Premium = 2`)
- Use explicit values for all enum members
- Store as strings in database via `.HasConversion<string>()` (see Database Conventions)
- Reserve high values (e.g., `99`) for special/system-level entries

## Error Handling

- Throw domain exceptions (`DomainException` subclasses) from Application/Domain layers
- `ExceptionHandlingMiddleware` maps them to RFC 7807 `ProblemDetails` — add new mappings there
- Controllers never catch exceptions; let the middleware handle them

## Logging Levels

- `LogInformation` — major pipeline steps (upload started, analysis complete)
- `LogDebug` — intermediate data (JSON previews, state transitions)
- `LogWarning` — recoverable issues (NTRP parsing fallback, missing optional fields)
- `LogError` — exceptions with full context
