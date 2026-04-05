# AISportCoach

AI-powered tennis coaching platform. Users upload videos; Gemini analyses them via Semantic Kernel and returns structured coaching reports with NTRP ratings.

## WHAT — Tech Stack & Structure

**Stack:** .NET 10 · C# 13 · ASP.NET Core · EF Core 10 · PostgreSQL · Semantic Kernel 1.x · Google Gemini (via File API) · .NET Aspire · MediatR 12 · xUnit · Moq

**Structure:**
- `aspire/` — Aspire AppHost (orchestration) + ServiceDefaults (OTEL, health, resilience)
- `src/AISportCoach.Domain` — Entities, enums, domain exceptions — zero external deps
- `src/AISportCoach.Application` — CQRS use cases, interfaces, SK plugins, orchestrator
- `src/AISportCoach.Infrastructure` — EF Core, repositories, Gemini File API client
- `src/AISportCoach.API` — Controllers, DTOs, mappers, middleware
- `tests/AISportCoach.IntegrationTests` — Integration tests against real Gemini API

**Dependency flow:** `API → Application → Domain ← Infrastructure`

## WHY — Architecture Philosophy

Clean Architecture with CQRS:
- **Domain** has zero deps — pure business logic and domain rules
- **Application** orchestrates use cases; defines interfaces that Infrastructure implements
- **Infrastructure** is a plug-in detail — never referenced by Application or Domain
- **CQRS via MediatR** — decouples handlers from HTTP, one handler per use case
- **Repository pattern** — Application never touches EF Core directly
- **SK Orchestrator** — single entry point for all AI pipeline execution; plugins are called explicitly, not auto-discovered

## HOW — Commands & Workflow

```bash
# Run (Aspire spins up Postgres + API)
dotnet run --project aspire/AISportCoach.AppHost

# Test (requires Gemini:ApiKey in tests/AISportCoach.IntegrationTests/appsettings.test.json)
dotnet test

# Add EF migration (run from solution root)
dotnet ef migrations add <Name> --project src/AISportCoach.Infrastructure --startup-project src/AISportCoach.API

# Format
dotnet format
```

Migrations apply automatically on startup in Development.

## Configuration

| Key | Required | Notes |
|-----|----------|-------|
| `Gemini:ApiKey` | Yes | Use User Secrets locally |
| `Gemini:ModelId` | Yes | Default: `gemini-2.5-flash` |
| `VideoStorage:MaxFileSizeMB` | No | Default: 500 |
| `VideoStorage:AllowedExtensions` | No | Default: `.mp4 .mov .avi .mkv` |
| `ConnectionStrings:tenniscoach` | Yes | Injected by Aspire |

Never commit secrets. Use User Secrets (dev) or environment variables (prod).

## Detailed Rules

@.claude/rules/architecture.md
@.claude/rules/api-conventions.md
@.claude/rules/database.md
@.claude/rules/ai-pipeline.md
@.claude/rules/testing.md
