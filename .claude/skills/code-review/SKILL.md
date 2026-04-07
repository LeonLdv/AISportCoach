---
name: code-review
description: Self-review recently changed or written code for correctness, quality, and adherence to project conventions. Use after writing or modifying code.
allowed-tools: Read Grep Glob Bash
---

You are performing a structured self-review of the code you just wrote or modified.

## Step 1 — Identify What Changed

Use `git diff HEAD` (or `git diff --staged`) to see all changed files. List them before proceeding.

## Step 2 — Review Each File Against These Checklists

### Architecture & Layer Boundaries
- [ ] Domain layer has zero NuGet deps beyond BCL
- [ ] Application layer does not import any Infrastructure types
- [ ] Infrastructure never references API project
- [ ] Handlers never call other handlers
- [ ] EF `DbSet` methods only called inside repository implementations
- [ ] `SaveChangesAsync` only called inside repository implementations

### CQRS / MediatR
- [ ] Commands and Queries are `record` types ending in `Command` / `Query`
- [ ] One handler per command/query
- [ ] Handler files live under `Application/UseCases/{FeatureName}/`

### C# Conventions
- [ ] Primary constructors used for DI (not manual field assignment)
- [ ] `record` types for DTOs, commands, queries, value objects
- [ ] File-scoped namespaces used
- [ ] No `.Result`, `.Wait()`, or `async void`
- [ ] `CancellationToken` accepted and forwarded everywhere I/O is performed
- [ ] Structured logging placeholders `{VarName}` — not string interpolation
- [ ] No `!` null-forgiving suppressors without an explanatory comment
- [ ] No exceptions used for control flow — domain exceptions only for domain errors
- [ ] No single-letter or abbreviated variable names — use descriptive names that convey intent (e.g., `apiKey` not `k`, `response` not `r`)

### API Conventions (if API layer touched)
- [ ] Controller has `[ApiController]`, `[Route]`, `[ApiVersion]`, `[Produces]`, `[Tags]`
- [ ] Actions have `[EndpointSummary]`, `[EndpointDescription]`, all `[ProducesResponseType]`
- [ ] Zero business logic in controllers — dispatch to MediatR only
- [ ] DTOs are `record` types in `DTOs/`
- [ ] Mapping done via `ToDto()` extension methods in `Mappers/`, not inline

### Database (if Infrastructure/Persistence touched)
- [ ] Entity has its own `IEntityTypeConfiguration<T>` in `Configurations/`
- [ ] `HasMaxLength` on every string column
- [ ] Enums stored as strings via `.HasConversion<string>()`
- [ ] No `EnsureCreated` — migrations only

### AI Pipeline (if SK plugins / orchestrator touched)
- [ ] Model ID and API key read from config — never hardcoded
- [ ] Plugin methods decorated with `[KernelFunction]`
- [ ] LLM markdown fences stripped before deserialisation (`StripToJson`)
- [ ] Polling retries capped; warnings logged when limit reached
- [ ] Orchestrator is the sole entry point — handlers never call plugins directly

### Security
- [ ] No secrets or API keys in code or config files
- [ ] No SQL string concatenation / injection risk
- [ ] No unvalidated user input passed to shell commands

## Step 3 — Report Findings

Format findings as:

```
FILE: <path>:<line>
ISSUE: <what is wrong>
FIX: <what to change>
```

If there are no issues, say: "No issues found — code looks good."

## Step 4 — Apply Fixes

For each issue found, apply the fix using the Edit tool. After all fixes are applied, re-read the changed sections to confirm they are correct.
