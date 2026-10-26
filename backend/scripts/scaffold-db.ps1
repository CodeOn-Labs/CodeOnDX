# Regenerate EF Core entities + DxDbContext from the database (DB-first).
# Run after adding a database/migrations/V0xx__*.sql script and applying it (Dx.Api --migrate).
#
#   powershell -ExecutionPolicy Bypass -File backend\scripts\scaffold-db.ps1
#   powershell -ExecutionPolicy Bypass -File backend\scripts\scaffold-db.ps1 -ConnectionString "Host=localhost;Port=5432;Database=dx;Username=dx_owner;Password=..."
#
# Overwrites: src\Dx.Domain\Entities\*.cs and src\Dx.Infrastructure\Persistence\DxDbContext.cs.
# Hand-written code stays in DxDbContext.Custom.cs and in partial classes outside Entities\.
param(
    [string]$ConnectionString = $env:DX_OWNER_CONNECTION
)
$ErrorActionPreference = "Stop"
$backend = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    $secrets = Join-Path $backend "src\Dx.Api\appsettings.Secrets.json"
    if (Test-Path $secrets) { $ConnectionString = (Get-Content $secrets -Raw | ConvertFrom-Json).ConnectionStrings.DxOwner }
}
if ([string]::IsNullOrWhiteSpace($ConnectionString)) { throw "Pass -ConnectionString or set ConnectionStrings:DxOwner in appsettings.Secrets.json" }

Push-Location $backend
try {
    dotnet tool restore
    # Entity files of dropped tables are not deleted automatically: remove them by hand.
    dotnet ef dbcontext scaffold $ConnectionString Npgsql.EntityFrameworkCore.PostgreSQL `
        --project src\Dx.Infrastructure --startup-project src\Dx.Api `
        --output-dir ..\Dx.Domain\Entities --namespace Dx.Domain.Entities `
        --context-dir Persistence --context DxDbContext --context-namespace Dx.Infrastructure.Persistence `
        --schema dx --no-onconfiguring --force
    if ($LASTEXITCODE -ne 0) { throw "Scaffold failed" }
    Write-Host "Done. Build the solution and review the diff of src\Dx.Domain\Entities."
}
finally {
    Pop-Location
}
