# AI Sport Coach

An AI-powered tennis coaching API that analyzes player videos and generates detailed coaching reports using Google Gemini and Microsoft Semantic Kernel.

## What it does

1. **Upload** a tennis video (`POST /api/v1/videos`)
2. **Analyze** it (`POST /api/v1/videos/{id}/analyze`) — the video is sent to the Gemini File API, then a Semantic Kernel agent pipeline evaluates technique, assigns an NTRP rating, and produces a structured coaching report
3. **Retrieve** reports (`GET /api/v1/reports/{id}`)

Reports include: NTRP skill rating, technique observations per stroke, improvement recommendations with drill suggestions, and an executive summary.

## Tech stack

| Layer | Technology |
|---|---|
| API | ASP.NET Core 10, MediatR, API versioning |
| AI orchestration | Microsoft Semantic Kernel, Google Gemini 2.5 Flash |
| Persistence | PostgreSQL + EF Core 10 |
| Infrastructure | .NET Aspire, Docker |
| API docs | Swashbuckle (`/swagger`) |

## Getting started

### Prerequisites
- .NET 10 SDK
- Docker (for PostgreSQL via Aspire)
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
  AISportCoach.Application/   # Use cases (MediatR), SK agent, plugins
  AISportCoach.Domain/        # Entities, enums, exceptions
  AISportCoach.Infrastructure/ # EF Core, Gemini file service, repositories
aspire/
  AISportCoach.AppHost/       # Aspire orchestration (PostgreSQL, service wiring)
```
