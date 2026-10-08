using System.Security.Cryptography;
using System.Text;

namespace DataGuard.Core.Models;

/// <summary>
/// Execution context supplied to validation rules containing database connections and cancellation.
/// </summary>
public record RuleContext
{
    public required Guid RunId { get; init; }
    public string? SourceConnectionString { get; init; }
    public required string TargetConnectionString { get; init; }
    public CancellationToken CancellationToken { get; init; } = default;
}

/// <summary>
/// Generates deterministic SHA-256 fingerprints across rules and identifiers.
/// </summary>
public static class FingerprintGenerator
{
    public static string Generate(string ruleId, string targetTable, string? targetColumn)
    {
        var raw = $"{ruleId.Trim().ToLowerInvariant()}|{targetTable.Trim().ToLowerInvariant()}|{(targetColumn ?? string.Empty).Trim().ToLowerInvariant()}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToUpperInvariant();
    }
}

/// <summary>
/// Domain model for tracked defects.
/// </summary>
public record Defect
{
    public Guid DefectId { get; init; } = Guid.NewGuid();
    public required string Fingerprint { get; init; }
    public required string RuleId { get; init; }
    public required string TargetTable { get; init; }
    public string? TargetColumn { get; init; }
    public required Enums.RuleSeverity Severity { get; init; }
    public Enums.DefectStatus Status { get; init; } = Enums.DefectStatus.Open;
    public required Guid FirstSeenRunId { get; init; }
    public required Guid LastSeenRunId { get; init; }
    public int OccurrenceCount { get; init; } = 1;
    public int RegressionCount { get; init; } = 0;
    public Guid? ResolvedRunId { get; init; }
    public DateTime FirstSeenAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime LastSeenAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime? ResolvedAtUtc { get; init; }
}

/// <summary>
/// High-level metrics for a validation run.
/// </summary>
public record RunSummary
{
    public required Guid RunId { get; init; }
    public string TriggeredBy { get; init; } = "System";
    public Enums.RunStatus Status { get; init; }
    public int TotalRules { get; init; }
    public int PassedRules { get; init; }
    public int FailedRules { get; init; }
    public int ErrorRules { get; init; }
    public decimal PassRatePercent => TotalRules > 0 ? Math.Round(((decimal)PassedRules / TotalRules) * 100m, 2) : 0m;
    public DateTime StartedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
    public long? DurationMs { get; init; }
    public int? ExitCode { get; init; }
    public string? SourceConnectionName { get; init; }
    public string? TargetConnectionName { get; init; }
    public IReadOnlyList<RuleResult> Results { get; init; } = Array.Empty<RuleResult>();
}
