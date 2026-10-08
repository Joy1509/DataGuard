# Helper script to launch DataGuard API using .NET 8
$env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet"
$env:PATH = "$env:USERPROFILE\.dotnet;$env:PATH"

Write-Host "Starting DataGuard Web API on http://localhost:5000..." -ForegroundColor Cyan
& "$env:USERPROFILE\.dotnet\dotnet.exe" run --project src/DataGuard.Api/DataGuard.Api.csproj --urls "http://localhost:5000"
