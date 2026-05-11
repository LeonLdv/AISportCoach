# AISportCoach

# CLAUDE.md

Behavioral guidelines to reduce common LLM coding mistakes. Merge with project-specific instructions as needed.

**Tradeoff:** These guidelines bias toward caution over speed. For trivial tasks, use judgment.

## 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:
- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

## 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

## 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:
- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:
- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

## 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:
- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:
```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

---

**These guidelines are working if:** fewer unnecessary changes in diffs, fewer rewrites due to overcomplication, and clarifying questions come before implementation rather than after mistakes.




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
| `Gemini:HttpTimeoutMinutes` | No | Default: 10. HttpClient and resilience pipeline timeouts for Gemini File API uploads |
| `VideoStorage:MaxFileSizeMB` | No | Default: 500. Also drives Kestrel `MaxRequestBodySize` and `FormOptions.MultipartBodyLengthLimit` |
| `VideoStorage:AllowedExtensions` | No | Default: `.mp4 .mov .avi .mkv` |
| `ConnectionStrings:tenniscoach` | Yes | Injected by Aspire |

Never commit secrets. Use User Secrets (dev) or environment variables (prod).

## Detailed Rules

@.claude/rules/architecture.md
@.claude/rules/api-conventions.md
@.claude/rules/database.md
@.claude/rules/ai-pipeline.md
@.claude/rules/testing.md
"" 
