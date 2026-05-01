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

## Primary Keys & GUIDs

**Always use UUIDv7 (time-ordered) for database primary keys:**

```csharp
// ✅ Correct - use UUIDv7 for entity IDs
public static VideoUpload Create(...)
{
    return new VideoUpload
    {
        Id = Guid.CreateVersion7(), // Time-ordered, index-friendly
        ...
    }
}

// ✅ Correct - Identity users must also use UUIDv7
var user = new ApplicationUser
{
    Id = Guid.CreateVersion7(), // Explicitly set before CreateAsync
    UserName = email,
    Email = email
};
await userManager.CreateAsync(user, password);

// ❌ Wrong - never use random GUIDs for database IDs
Id = Guid.NewGuid() // Random v4 - causes index fragmentation

// ✅ Exception - fixed GUIDs OK for seed data only
var systemUserId = new Guid("00000000-0000-0000-0000-000000000001");
```

**Why UUIDv7:**
- Time-ordered (timestamp in first 48 bits) → sequential inserts
- Minimal B-tree index fragmentation (~5-10% vs 100% with v4)
- Better cache locality and query performance
- Safe for distributed systems (unlike auto-increment integers)
- PostgreSQL indexes perform well with sequential UUIDs

**Never:**
- Use `Guid.NewGuid()` (UUIDv4) for entity IDs — causes severe index fragmentation
- Use auto-increment integers for primary keys in this codebase — breaks the established pattern

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
