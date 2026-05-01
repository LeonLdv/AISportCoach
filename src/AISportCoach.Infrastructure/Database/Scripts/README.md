# Database SQL Scripts

SQL migration files for AISportCoach database schema.

## Files

- `migrations.sql` - Core database schema
- `migrations_*.sql` - Timestamped migration backups

## Notes

Database migrations are managed by EF Core and apply automatically in Development mode (see `Program.cs`).
These SQL files are provided for manual migration scenarios or production deployments.

For automated migration scripts, see `/scripts/migrate-db.ps1` at the solution root.
