# Database Migration Script (PowerShell)
# Generates SQL script first, then applies migration to database

$ErrorActionPreference = "Stop"

Write-Host "===================================" -ForegroundColor Cyan
Write-Host "Database Migration Script" -ForegroundColor Cyan
Write-Host "===================================" -ForegroundColor Cyan
Write-Host ""

# Generate timestamp for filename
$timestamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
$sqlFile = "db/migrations/migrations_$timestamp.sql"

# Step 1: Generate SQL migration script
Write-Host "Step 1: Generating SQL migration script..." -ForegroundColor Yellow
dotnet ef migrations script `
  --project src/AISportCoach.Infrastructure `
  --startup-project src/AISportCoach.API `
  --output $sqlFile `
  --idempotent

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ SQL script generated successfully: $sqlFile" -ForegroundColor Green
    Write-Host ""
} else {
    Write-Host "❌ Failed to generate SQL script" -ForegroundColor Red
    exit 1
}

# Step 2: Show SQL script preview
Write-Host "Step 2: SQL Script Preview (first 50 lines):" -ForegroundColor Yellow
Write-Host "-----------------------------------" -ForegroundColor Gray
Get-Content $sqlFile -TotalCount 50
Write-Host "..." -ForegroundColor Gray
Write-Host "-----------------------------------" -ForegroundColor Gray
Write-Host ""

# Step 3: Confirm before applying
$confirm = Read-Host "Do you want to apply this migration to the database? (y/N)"

if ($confirm -ne "y" -and $confirm -ne "Y") {
    Write-Host "Migration cancelled. SQL script saved to: $sqlFile" -ForegroundColor Yellow
    exit 0
}

# Step 4: Apply migration to database
Write-Host ""
Write-Host "Step 3: Applying migration to database..." -ForegroundColor Yellow
dotnet ef database update `
  --project src/AISportCoach.Infrastructure `
  --startup-project src/AISportCoach.API

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "✅ Database migration completed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Summary:" -ForegroundColor Cyan
    Write-Host "  - SQL script: $sqlFile" -ForegroundColor White
    Write-Host "  - Database: Updated" -ForegroundColor White
} else {
    Write-Host ""
    Write-Host "❌ Database migration failed" -ForegroundColor Red
    exit 1
}
