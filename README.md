# AISportCoach

AI-powered tennis coaching platform. Upload videos of your tennis sessions and receive structured coaching reports with NTRP skill ratings, technique analysis, and personalized drill recommendations — powered by Google Gemini via Microsoft Semantic Kernel.

## Features

### 🎾 Video Analysis
- **Upload** tennis videos via multipart form (`POST /api/v1/videos`)
- **Analyze** technique using Gemini's multimodal vision (`POST /api/v1/videos/{id}/analyze`)
- **Generate reports** with NTRP rating (1.5–7.0 scale), stroke-by-stroke observations, improvement areas, and drill suggestions
- **History-aware coaching** — retrieves your 5 most similar past sessions via semantic search and highlights trends (improvements, regressions, recurring issues)

### 💬 Ask the Coach (RAG)
- **Natural language Q&A** (`POST /api/v1/coach/ask`) — ask questions about your past performance
- Answers grounded in your coaching report history via **pgvector** semantic search (cosine similarity over Gemini embeddings)
- Example: *"What has been my biggest weakness in the last month?"*

### 📊 Retrieve Reports
- **Fetch individual reports** (`GET /api/v1/reports/{id}`)
- **List all reports** (`GET /api/v1/reports`) with pagination and player filtering

## Architecture

**Clean Architecture + CQRS** with strict layer boundaries:

```
┌─────────────────────────────────────────┐
│  API Layer (Controllers, DTOs)          │
└────────────────┬────────────────────────┘
                 │ MediatR Commands/Queries
┌────────────────▼────────────────────────┐
│  Application Layer                      │
│  • CQRS handlers (MediatR)              │
│  • TennisCoachOrchestrator              │
│  • SK Plugins (VideoAnalysis,           │
│    ReportGeneration, NtrpRating)        │
│  • Interfaces (IVideoRepository, etc.)  │
└────────────────┬────────────────────────┘
                 │
        ┌────────┴────────┐
        ▼                 ▼
┌───────────────┐  ┌──────────────────────┐
│ Domain        │  │ Infrastructure       │
│ • Entities    │  │ • EF Core + Postgres │
│ • Enums       │  │ • Repositories       │
│ • Exceptions  │  │ • Gemini File API    │
│ (zero deps)   │  │ • Embedding service  │
└───────────────┘  └──────────────────────┘
```

**Key principles:**
- Domain has zero external dependencies — pure business logic
- Application defines interfaces; Infrastructure implements them
- One MediatR handler per use case
- Repository pattern — no direct EF Core access in handlers
- Orchestrator explicitly calls SK plugins (no auto-discovery)

## AI Pipeline

```
Upload video
    │
    ▼
1. GeminiFileService uploads to Gemini File API (resumable)
    │
    ▼
2. VideoAnalysisPlugin
   └─▶ Extracts technique observations via Gemini multimodal vision
    │
    ▼
3. Retrieve 5 most similar past sessions (pgvector cosine similarity)
    │
    ▼
4. ReportGenerationPlugin + NtrpRatingPlugin (parallel)
   ├─▶ Generates coaching report with trends from history
   └─▶ Assigns evidence-based NTRP rating with justification
    │
    ▼
5. Generate embedding for new report (Gemini text-embedding-004)
    │
    ▼
6. Save report + embedding to PostgreSQL (pgvector)
```

## Tech Stack

| Layer | Technology |
|-------|-----------|
| **Framework** | .NET 10, C# 13, ASP.NET Core |
| **AI** | Microsoft Semantic Kernel 1.x, Google Gemini 2.5 Flash |
| **Semantic Search** | pgvector (HNSW index), Gemini text-embedding-004 (768-dim) |
| **Database** | PostgreSQL 17 + EF Core 10 |
| **Architecture** | MediatR 12 (CQRS), Clean Architecture, Repository pattern |
| **Orchestration** | .NET Aspire 9 (AppHost + ServiceDefaults) |
| **Testing** | xUnit, Moq, integration tests against real Gemini API |
| **API Docs** | Swashbuckle/Swagger (`/swagger`) |

## Getting Started

### Prerequisites
- **.NET 10 SDK** — [download](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Docker Desktop** — for PostgreSQL + pgvector (managed by Aspire)
- **Gemini API key** — [get one here](https://aistudio.google.com/app/apikey)

### Configuration

Add your Gemini API key to user secrets:

```bash
dotnet user-secrets set "Gemini:ApiKey" "YOUR_API_KEY_HERE" --project src/AISportCoach.API
```

Optional configuration (uses defaults if not set):

| Key | Default | Notes |
|-----|---------|-------|
| `Gemini:ModelId` | `gemini-2.5-flash` | Gemini model to use |
| `VideoStorage:MaxFileSizeMB` | `500` | Max upload size |
| `VideoStorage:AllowedExtensions` | `.mp4 .mov .avi .mkv` | Allowed video formats |

### Run

Start the application with Aspire (auto-provisions PostgreSQL + pgvector):

```bash
dotnet run --project aspire/AISportCoach.AppHost
```

- **API:** `https://localhost:{port}`
- **Swagger UI:** `https://localhost:{port}/swagger`
- **Aspire Dashboard:** `http://localhost:15xxx` (shown in terminal)

Migrations apply automatically on startup in Development mode.

### Test

Integration tests require a Gemini API key in `appsettings.test.json`:

```bash
# Add API key to test project
dotnet user-secrets set "Gemini:ApiKey" "YOUR_API_KEY_HERE" --project tests/AISportCoach.IntegrationTests

# Run tests (hits real Gemini API)
dotnet test
```

### Commands

```bash
# Format code
dotnet format

# Add EF Core migration (run from solution root)
dotnet ef migrations add MigrationName \
  --project src/AISportCoach.Infrastructure \
  --startup-project src/AISportCoach.API
```

## Project Structure

```
src/
  AISportCoach.API/
    Controllers/          # VideosController, ReportsController, CoachController
    DTOs/                 # Request/response records
    Mappers/              # Entity → DTO extension methods
    Middleware/           # Exception handling, request logging
  AISportCoach.Application/
    UseCases/             # CQRS commands/queries + handlers
    Agents/               # TennisCoachOrchestrator
    Plugins/              # SK plugins (VideoAnalysis, ReportGeneration, NtrpRating)
    Interfaces/           # Repository interfaces
  AISportCoach.Domain/
    Entities/             # VideoUpload, CoachingReport (zero external deps)
    Enums/                # PlayerLevel, VideoStatus, ProcessingState
    Exceptions/           # Domain-specific exceptions
  AISportCoach.Infrastructure/
    Persistence/          # AppDbContext, EF configurations, repositories
    VideoProcessing/      # GeminiFileService (File API client)
    AI/                   # GeminiEmbeddingService (text-embedding-004)

aspire/
  AISportCoach.AppHost/       # Orchestration, service wiring
  AISportCoach.ServiceDefaults/ # OTEL, health checks, resilience

tests/
  AISportCoach.IntegrationTests/ # Integration tests (real Gemini API)
  AISportCoach.FunctionalTests/  # End-to-end tests

.claude/
  rules/                  # Architecture, API, database, AI, testing conventions
```

## Contributing

This project follows strict architectural conventions documented in `.claude/rules/`:

- **architecture.md** — layer boundaries, CQRS, C# conventions
- **api-conventions.md** — controller patterns, DTOs, validation
- **database.md** — EF Core configuration, migration workflow
- **ai-pipeline.md** — Semantic Kernel plugin authoring, orchestrator rules
- **testing.md** — test structure, what to mock vs not mock

See `CLAUDE.md` for a condensed overview.

## License

MIT
