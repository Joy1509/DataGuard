# DataGuard Benchmark Automation Tool
param(
    [string]$TargetConn = "Server=localhost;Database=MigratedDB;TrustServerCertificate=True;",
    [string]$SourceConn = "Server=localhost;Database=LegacyDB;TrustServerCertificate=True;",
    [string]$RulesFile = "rules.sample.json",
    [int]$Iterations = 3
)

Write-Host "===============================================" -ForegroundColor Cyan
Write-Host " DataGuard Benchmarking & Performance Harness " -ForegroundColor Cyan
Write-Host "===============================================" -ForegroundColor Cyan

$dotnet = "$env:USERPROFILE\.dotnet\dotnet.exe"
if (!(Test-Path $dotnet)) { $dotnet = "dotnet" }

# Measure Host Info
$cpu = (Get-CimInstance Win32_Processor).Name
$ram = [Math]::Round((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory / 1GB, 2)
Write-Host "Host CPU: $cpu" -ForegroundColor Gray
Write-Host "Host RAM: $ram GB" -ForegroundColor Gray

$configs = @(
    @{ Name = "Sequential (p=1)"; Parallelism = 1 },
    @{ Name = "Parallel (p=2)"; Parallelism = 2 },
    @{ Name = "Parallel (p=4)"; Parallelism = 4 },
    @{ Name = "Parallel (p=8)"; Parallelism = 8 }
)

$results = @()

foreach ($cfg in $configs) {
    Write-Host "`nBenchmarking Configuration: $($cfg.Name)..." -ForegroundColor Yellow
    $durations = @()

    for ($i = 1; $i -le $Iterations; $i++) {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        & $dotnet run --project src/DataGuard.Cli/DataGuard.Cli.csproj --no-build -- run `
            --rules $RulesFile `
            --source $SourceConn `
            --target $TargetConn `
            --parallelism $($cfg.Parallelism) `
            --format json | Out-Null
        $sw.Stop()
        $durations += $sw.ElapsedMilliseconds
        Write-Host "  Iteration $i: $($sw.ElapsedMilliseconds) ms" -ForegroundColor Gray
    }

    $avg = [Math]::Round(($durations | Measure-Object -Average).Average, 2)
    $min = ($durations | Measure-Object -Minimum).Minimum
    $max = ($durations | Measure-Object -Maximum).Maximum

    $results += [PSCustomObject]@{
        Configuration = $cfg.Name
        Parallelism = $cfg.Parallelism
        AvgDurationMs = $avg
        MinDurationMs = $min
        MaxDurationMs = $max
    }
}

Write-Host "`n=== Benchmark Summary ===" -ForegroundColor Green
$results | Format-Table -AutoSize
