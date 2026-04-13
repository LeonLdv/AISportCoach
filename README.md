# AI Sport Coach

An AI-powered tennis coaching API that analyzes player videos and generates detailed coaching reports using Google Gemini and Microsoft Semantic Kernel.

## What it does

1. **Upload** a tennis video (`POST /api/v1/videos`)
2. **Analyze** it (`POST /api/v1/videos/{id}/analyze`) — the video is sent to the Gemini File API, then a Semantic Kernel pipeline evaluates technique, assigns an NTRP rating, and produces a structured coaching report
3. **Retrieve** reports (`GET /api/v1/reports/{id}`)
4. **Ask the coach** (`POST /api/v1/coach/ask`) — ask natural language questions about past sessions; answers are grounded in the player's own report history via semantic search

Reports include: evidence-based NTRP skill rating (1.5–7.0 scale), technique observations per stroke, improvement recommendations with drill suggestions, and an executive summary.

## AI pipeline

```
Upload video
    └─▶ VideoAnalysisPlugin        — Gemini multimodal: extracts technique observations from video
            └─▶ ReportGenerationPlugin  — generates scored coaching report with recommendations
            └─▶ NtrpRatingPlugin        — assigns evidence-based NTRP rating with justification
                    └─▶ Embedding saved to pgvector  — enables history-aware coaching
```

On each new analysis the pipeline retrieves the player's 5 most similar past sessions (cosine similarity over pgvector) and weaves trend observations — improvements, regressions, recurring issues — into the report.

## Tech stack

| Layer | Technology |
|---|---|
| API | ASP.NET Core 10, MediatR, API versioning |
| AI orchestration | Microsoft Semantic Kernel, Google Gemini 2.5 Flash |
| Semantic search | pgvector (cosine similarity), Gemini text-embedding-004 |
| Persistence | PostgreSQL + EF Core 10 |
| Infrastructure | .NET Aspire, Docker |
| API docs | Swashbuckle (`/swagger`) |

## Getting started

### Prerequisites
- .NET 10 SDK
- Docker (for PostgreSQL + pgvector via Aspire)
- Gemini API key

### Configuration

Add your API key to user secrets:

```bash
dotnet user-secrets set "Gemini:ApiKey" "<your-key>" --project src/AISportCoach.API
```

### Run

```bash
dotnet run --project aspire/AISportCoach.AppHost
```

The API will be available at `https://localhost:{port}` with Swagger UI at `/swagger`.

## Project structure

```
src/
  AISportCoach.API/           # Controllers, DTOs, middleware
  AISportCoach.Application/   # Use cases (MediatR), SK orchestrator, plugins
  AISportCoach.Domain/        # Entities, enums, exceptions
  AISportCoach.Infrastructure/ # EF Core, Gemini file/embedding services, repositories
aspire/
  AISportCoach.AppHost/       # Aspire orchestration (PostgreSQL + pgvector, service wiring)
```
