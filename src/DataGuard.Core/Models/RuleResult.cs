using DataGuard.Core.Enums;

namespace DataGuard.Core.Models;

/// <summary>
/// Immutable outcome of executing a single validation rule.
/// </summary>
public record RuleResult
{
    public required string RuleId { get; init; }
    public required string RuleName { get; init; }
    public required RuleType RuleType { get; init; }
    public required RuleSeverity Severity { get; init; }
    public required string TargetTable { get; init; }
    public string? TargetColumn { get; init; }
    public required string Fingerprint { get; init; }
    public required RuleStatus Status { get; init; }
    public required long ExecutionTimeMs { get; init; }
    public string? SourceValue { get; init; }
    public string? TargetValue { get; init; }
    public long DiscrepancyCount { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<string> SampleRowsJson { get; init; } = Array.Empty<string>();
    public DateTime ExecutedAtUtc { get; init; } = DateTime.UtcNow;

    public static RuleResult Pass(
        RuleDefinition definition,
        string fingerprint,
        long executionTimeMs,
        string? sourceVal = null,
        string? targetVal = null) =>
        new()
        {
            RuleId = definition.Id,
            RuleName = definition.Name,
            RuleType = definition.Type,
            Severity = definition.Severity,
            TargetTable = definition.TargetTable,
            TargetColumn = definition.TargetColumn,
            Fingerprint = fingerprint,
            Status = RuleStatus.Passed,
            ExecutionTimeMs = executionTimeMs,
            SourceValue = sourceVal,
            TargetValue = targetVal,
            DiscrepancyCount = 0
        };

    public static RuleResult Fail(
        RuleDefinition definition,
        string fingerprint,
        long executionTimeMs,
        long discrepancyCount,
        string? sourceVal = null,
        string? targetVal = null,
        IReadOnlyList<string>? sampleRows = null,
        string? error = null) =>
        new()
        {
            RuleId = definition.Id,
            RuleName = definition.Name,
            RuleType = definition.Type,
            Severity = definition.Severity,
            TargetTable = definition.TargetTable,
            TargetColumn = definition.TargetColumn,
            Fingerprint = fingerprint,
            Status = RuleStatus.Failed,
            ExecutionTimeMs = executionTimeMs,
            SourceValue = sourceVal,
            TargetValue = targetVal,
            DiscrepancyCount = discrepancyCount,
            SampleRowsJson = sampleRows ?? Array.Empty<string>(),
            ErrorMessage = error
        };

    public static RuleResult Error(
        RuleDefinition definition,
        string fingerprint,
        long executionTimeMs,
        string errorMessage) =>
        new()
        {
            RuleId = definition.Id,
            RuleName = definition.Name,
            RuleType = definition.Type,
            Severity = definition.Severity,
            TargetTable = definition.TargetTable,
            TargetColumn = definition.TargetColumn,
            Fingerprint = fingerprint,
            Status = RuleStatus.Error,
            ExecutionTimeMs = executionTimeMs,
            DiscrepancyCount = 0,
            ErrorMessage = errorMessage
        };
}
