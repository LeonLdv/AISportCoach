# Spec: Aspire Integration Tests for Video Upload Endpoint

## Context

The project currently has no HTTP-level integration tests. The existing `IntegrationTests` project tests Gemini API directly (no HTTP, no controllers, no middleware). We need true end-to-end functional tests that exercise the full HTTP pipeline: `HTTP request -> Controller -> MediatR -> Handler -> Repository -> Postgres -> Response`.

.NET Aspire's testing infrastructure (`Aspire.Hosting.Testing`) spins up real containers (Postgres) and real API processes, giving us true distributed integration tests. This plan creates a new `FunctionalTests` project using this approach.

## Test Specification

### Endpoint: `POST /api/v1/videos` (Upload)

| # | Test Name | Preconditions | Input | Expected | Gemini Needed? |
|---|-----------|---------------|-------|----------|----------------|
| 1 | `Upload_EmptyFile_Returns400` | None | 0-byte .mp4 file | 400 + ValidationProblemDetails with `file` error | No |
| 2 | `Upload_NoFileInRequest_Returns422` | None | Empty multipart (no `file` field) | 422 (model binding failure) | No |
| 3 | `Upload_UnsupportedExtension_Returns400` | Gemini:ApiKey configured | 1KB .txt file | 400 + ProblemDetails mentioning ".txt" | Yes (handler DI) |
| 4 | `Upload_ValidMp4_Returns201` | Gemini:ApiKey configured | 2KB .mp4 file | 201 + VideoResponseDto + Location header | Yes (real API call) |
| 5 | `GetById_AfterUpload_Returns200` | Test 4 succeeded | GET by ID from upload response | 200 + matching VideoResponseDto | Yes (prerequisite) |
| 6 | `GetById_NonExistentId_Returns404` | None | Random GUID | 404 + ProblemDetails | No |

### Why the split?

`VideoFileService` constructor throws `InvalidOperationException` if `Gemini:ApiKey` is empty. Tests 1-2 and 6 never invoke MediatR handlers that depend on `IVideoFileService`, so they work without a key. Tests 3-5 resolve `UploadVideoHandler` which depends on `IVideoFileService` via DI.

## Implementation Plan

### Step 1: Create `tests/AISportCoach.FunctionalTests/AISportCoach.FunctionalTests.csproj`

New test project with:
- `Aspire.Hosting.Testing` **v13.1.3** (must match AppHost SDK `Aspire.AppHost.Sdk/13.1.3`)
- `xunit 2.9.3`, `Microsoft.NET.Test.Sdk 17.14.1`, `xunit.runner.visualstudio 3.1.4`
- Project reference -> `aspire/AISportCoach.AppHost` (provides `Projects.AISportCoach_AppHost` type)
- No references to `src/` projects (API is an opaque process in Aspire testing)

### Step 2: Add project to `AISportCoach.slnx`

Add under `/tests/` folder alongside `IntegrationTests`.

### Step 3: Create `Fixtures/AspireFixture.cs`

Shared xUnit fixture implementing `IAsyncLifetime`:

1. `DistributedApplicationTestingBuilder.CreateAsync<Projects.AISportCoach_AppHost>()`
2. Set `builder.Configuration["Parameters:postgres-password"] = "test-password"` (AppHost requires this secret parameter)
3. `BuildAsync()` + `StartAsync()`
4. `WaitForResourceHealthyAsync("api")` with 120s timeout
5. `App.CreateHttpClient("api")` -> HttpClient with correct base URL

Key considerations:
- **Port conflict**: AppHost uses `.WithHostPort(5432)` -- will fail if local Postgres is running on 5432. Known limitation; future fix is removing the hardcoded port from AppHost.
- **Docker required**: Postgres runs as a real container via Docker.
- **Auto-migration**: API runs in Development mode by default, so `db.Database.MigrateAsync()` runs on startup.
- **pgAdmin container**: AppHost calls `.WithPgAdmin()` which adds unnecessary overhead. Not a blocker.

### Step 4: Create `Fixtures/AspireCollection.cs`

xUnit collection definition so all test classes share the single fixture (avoid restarting Postgres + API per class).

### Step 5: Create `VideoUploadTests.cs`

Six test methods using primary constructor `(AspireFixture fixture)`.
- Use `MultipartFormDataContent` + `ByteArrayContent` to build multipart requests
- Parse responses with `System.Text.Json.JsonDocument`
- Assert with plain xUnit `Assert.*`

### Step 6: Build and verify

```bash
dotnet build tests/AISportCoach.FunctionalTests
dotnet test tests/AISportCoach.FunctionalTests
```

## Files Created/Modified

| File | Action |
|------|--------|
| `tests/AISportCoach.FunctionalTests/AISportCoach.FunctionalTests.csproj` | **Create** |
| `tests/AISportCoach.FunctionalTests/Fixtures/AspireFixture.cs` | **Create** |
| `tests/AISportCoach.FunctionalTests/Fixtures/AspireCollection.cs` | **Create** |
| `tests/AISportCoach.FunctionalTests/VideoUploadTests.cs` | **Create** |
| `AISportCoach.slnx` | **Modify** -- add new project |

## Verification

1. `dotnet build` -- project compiles
2. `dotnet test tests/AISportCoach.FunctionalTests` with Docker running
3. Tests 1, 2, 6 pass without Gemini API key
4. Tests 3-5 pass with valid `Gemini:ApiKey` in API user secrets
5. Tests produce correct HTTP status codes and response bodies

## Known Limitations & Future Improvements

- **File-too-large test**: Default `MaxFileSizeMB` is 500MB -- impractical to send in a test. Requires `appsettings.Testing.json` with a lower value + environment override. Better as a handler-level unit test.
- **Port 5432 conflict**: Remove `.WithHostPort(5432)` from AppHost (Aspire service discovery doesn't need it).
- **pgAdmin overhead**: Conditionally skip `.WithPgAdmin()` in test environment.
- **Data volume persistence**: Named volume `tenniscoach-pgdata` may persist data across test runs.
