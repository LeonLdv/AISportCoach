# Testing Conventions

## Framework & Project

- xUnit 2.x + Moq 4.x in `tests/AISportCoach.UnitTests`
- Integration tests call the **real Gemini API** — no mocking the AI layer
- Requires `Gemini:ApiKey` in `tests/AISportCoach.UnitTests/appsettings.test.json` or env var

## What to Mock vs What Not To

**Mock** (via Moq):
- Repository interfaces (`IVideoRepository`, `ICoachingReportRepository`)
- `IVideoFileService`

**Never mock:**
- EF Core `DbContext` or `DbSet` — use real DB or in-memory for data tests
- `Kernel` or Semantic Kernel internals — test against the real API
- `TennisCoachOrchestrator` in integration tests — let it run fully

## Naming

- Class: `{Subject}Tests` (e.g., `TennisCoachAgentPipelineTests`)
- Method: `{Method}_{Scenario}_{ExpectedResult}`

```
ProcessAsync_ValidVideo_ReturnsCoachingReportWithNtrpRating
UploadAsync_FileTooLarge_ThrowsVideoTooLargeException
```

## Performance — Avoid Redundant Uploads

- Cache Gemini file URIs in a temp file during test runs
- Check cache before uploading; reuse existing URI if still ACTIVE
- See: `tests/AISportCoach.UnitTests/Integration/TennisCoachAgentPipelineTest.cs`

## Assertions

- Use plain xUnit `Assert.*` — no FluentAssertions or Shouldly
- Assert structure, not exact LLM text (LLM output is non-deterministic)
- For NTRP: assert range (`>= 1.5 && <= 7.0`), not exact value

## Patterns NOT Used Here

- No `WebApplicationFactory` — no HTTP-level integration tests yet
- No database fixtures — infrastructure tests use real Gemini, not real DB
- No test containers for PostgreSQL yet
