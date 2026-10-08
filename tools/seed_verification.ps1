# DataGuard Seed Verification Tool
param(
    [string]$Server = "localhost",
    [string]$LegacyDb = "LegacyDB",
    [string]$MigratedDb = "MigratedDB",
    [string]$RulesFile = "rules.sample.json",
    [string]$ManifestFile = "db/seed/injected-issues.json"
)

Write-Host "====================================================" -ForegroundColor Cyan
Write-Host " DataGuard Ground-Truth Seed Verification Pipeline " -ForegroundColor Cyan
Write-Host "====================================================" -ForegroundColor Cyan

$dotnet = "$env:USERPROFILE\.dotnet\dotnet.exe"
if (!(Test-Path $dotnet)) { $dotnet = "dotnet" }

Write-Host "[1/3] Validating rule suite syntax..." -ForegroundColor Yellow
& $dotnet run --project src/DataGuard.Cli/DataGuard.Cli.csproj -- validate-rules --file $RulesFile
if ($LASTEXITCODE -ne 0) {
    Write-Host "Rule validation failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "[2/3] Checking ground-truth manifest existence..." -ForegroundColor Yellow
if (!(Test-Path $ManifestFile)) {
    Write-Host "Manifest file $ManifestFile not found!" -ForegroundColor Red
    exit 2
}

Write-Host "[3/3] Executing seed verification run..." -ForegroundColor Yellow
& $dotnet run --project src/DataGuard.Cli/DataGuard.Cli.csproj -- verify-seed `
    --manifest $ManifestFile `
    --rules $RulesFile `
    --source "Server=$Server;Database=$LegacyDb;TrustServerCertificate=True;" `
    --target "Server=$Server;Database=$MigratedDb;TrustServerCertificate=True;"

Write-Host "Ground-truth seed verification process complete." -ForegroundColor Green
