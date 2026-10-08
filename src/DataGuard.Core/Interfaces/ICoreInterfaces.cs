using DataGuard.Core.Models;

namespace DataGuard.Core.Interfaces;

/// <summary>
/// Strategy contract for an executable validation rule.
/// </summary>
public interface IValidationRule
{
    RuleDefinition Definition { get; }
    Task<RuleResult> ExecuteAsync(RuleContext context, CancellationToken ct);
}

/// <summary>
/// Factory creating concrete IValidationRule instances from definitions.
/// </summary>
public interface IRuleFactory
{
    IValidationRule Create(RuleDefinition definition);
}

/// <summary>
/// SQL query execution abstraction allowing Core to remain free of direct SqlClient references.
/// </summary>
public interface ISqlExecutor
{
    Task<long> GetRowCountAsync(string connectionString, string tableName, string? filterCondition, int timeoutSeconds, CancellationToken ct);
    Task<decimal?> GetAggregateAsync(string connectionString, string tableName, string columnName, Enums.AggregateFunction function, string? filterCondition, int timeoutSeconds, CancellationToken ct);
    Task<long> GetChecksumAggAsync(string connectionString, string tableName, IReadOnlyList<string> columns, string? filterCondition, int timeoutSeconds, CancellationToken ct);
    Task<(long DiscrepancyCount, IReadOnlyList<string> BadRowsJson)> GetNullOrBlankRowsAsync(string connectionString, string tableName, IReadOnlyList<string> columns, bool allowBlank, int limit, int timeoutSeconds, CancellationToken ct);
    Task<(long DiscrepancyCount, IReadOnlyList<string> BadRowsJson)> GetDuplicateKeyRowsAsync(string connectionString, string tableName, IReadOnlyList<string> keyColumns, string? filterCondition, int limit, int timeoutSeconds, CancellationToken ct);
    Task<(long DiscrepancyCount, IReadOnlyList<string> BadRowsJson)> GetOrphanKeyRowsAsync(string connectionString, string childTable, string childColumn, string parentTable, string parentColumn, int limit, int timeoutSeconds, CancellationToken ct);
    Task<(long DiscrepancyCount, IReadOnlyList<string> BadRowsJson)> GetValueCheckRowsAsync(string connectionString, string tableName, string columnName, IReadOnlyList<string>? allowedValues, decimal? minVal, decimal? maxVal, string? pattern, int limit, int timeoutSeconds, CancellationToken ct);
    Task<(long DiscrepancyCount, IReadOnlyList<string> Discrepancies)> GetSchemaDriftAsync(string sourceConn, string sourceTable, string targetConn, string targetTable, int timeoutSeconds, CancellationToken ct);
    Task<(long ResultCount, IReadOnlyList<string> BadRowsJson)> ExecuteCustomSqlAsync(string connectionString, string selectSql, int limit, int timeoutSeconds, CancellationToken ct);
}

/// <summary>
/// Orchestrates parallel execution of validation rules with bounded concurrency.
/// </summary>
public interface IRuleRunner
{
    Task<IReadOnlyList<RuleResult>> RunSuiteAsync(
        IReadOnlyList<IValidationRule> rules,
        RuleContext context,
        int maxDegreeOfParallelism = 4,
        Action<RuleResult>? onRuleCompleted = null,
        CancellationToken ct = default);
}

/// <summary>
/// System clock provider abstraction.
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
}

/// <summary>
/// Notification dispatch abstraction.
/// </summary>
public interface INotifier
{
    Task SendSummaryAsync(RunSummary summary, CancellationToken ct = default);
}

/// <summary>
/// Issue tracker sync abstraction (e.g. GitHub Issues).
/// </summary>
public interface IIssueTracker
{
    Task SyncDefectsAsync(RunSummary summary, IReadOnlyList<Defect> defects, CancellationToken ct = default);
}

/// <summary>
/// Repository for validation rules.
/// </summary>
public interface IRuleRepository
{
    Task<IReadOnlyList<RuleDefinition>> GetAllAsync(CancellationToken ct = default);
    Task<RuleDefinition?> GetByIdAsync(string ruleId, CancellationToken ct = default);
    Task SaveAsync(RuleDefinition rule, CancellationToken ct = default);
    Task DeleteAsync(string ruleId, CancellationToken ct = default);
}

/// <summary>
/// Repository for validation run history and results.
/// </summary>
public interface IRunRepository
{
    Task<Guid> StartRunAsync(int? sourceConnId, int targetConnId, string triggeredBy, int totalRules, CancellationToken ct = default);
    Task<Guid> RecordResultAsync(Guid runId, RuleResult result, CancellationToken ct = default);
    Task CompleteRunAsync(Guid runId, int exitCode, CancellationToken ct = default);
    Task<RunSummary?> GetSummaryAsync(Guid? runId = null, CancellationToken ct = default);
    Task<IReadOnlyList<RunSummary>> GetRecentRunsAsync(int top = 30, CancellationToken ct = default);
}

/// <summary>
/// Repository for tracking defects across runs.
/// </summary>
public interface IDefectRepository
{
    Task<Defect> UpsertDefectAsync(string fingerprint, string ruleId, string targetTable, string? targetColumn, Enums.RuleSeverity severity, Guid runId, CancellationToken ct = default);
    Task CloseResolvedDefectsAsync(Guid runId, CancellationToken ct = default);
    Task<IReadOnlyList<Defect>> GetActiveDefectsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Defect>> GetAllDefectsAsync(Enums.DefectStatus? status = null, Enums.RuleSeverity? severity = null, CancellationToken ct = default);
}
