# Helper script to launch DataGuard CLI in Demo Mode
$env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet"
$env:PATH = "$env:USERPROFILE\.dotnet;$env:PATH"

& "$env:USERPROFILE\.dotnet\dotnet.exe" run --project src/DataGuard.Cli/DataGuard.Cli.csproj -- run --demo --format table
