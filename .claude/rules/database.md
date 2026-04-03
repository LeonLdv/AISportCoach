# Database Conventions

## EF Core Configuration

- Every entity has its own `IEntityTypeConfiguration<T>` in `Infrastructure/Persistence/Configurations/`
- Applied via `ApplyConfigurationsFromAssembly()` in `AppDbContext` — never inline in `OnModelCreating`
- See canonical example: `src/AISportCoach.Infrastructure/Persistence/Configurations/CoachingReportConfiguration.cs`

## Column Rules

- `HasMaxLength` on every string column — use the largest sensible value for the field's purpose
- Enum columns: `.HasConversion<string>()` — store as strings, not integers
- JSONB columns (e.g. `DrillSuggestions`): `HasColumnType("jsonb")`
- Optional navigations: map as nullable (`string?`, `Guid?`)

## Migrations

Run from solution root:
```bash
dotnet ef migrations add <Name> \
  --project src/AISportCoach.Infrastructure \
  --startup-project src/AISportCoach.API
```

Migrations apply automatically in Development (see `Program.cs`). Never call `EnsureCreated` — always use migrations.

## Access Rules

- EF `DbSet` methods are only called inside repository implementations
- No raw SQL unless EF cannot express the query, and only via `FromSqlRaw` / `ExecuteSqlRaw`
- `SaveChangesAsync` is called only inside repositories — never in handlers or services

## Patterns NOT Used Here

- No Unit of Work pattern — repositories call `SaveChangesAsync` directly
- No Dapper — EF Core only
- No stored procedures
