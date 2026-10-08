using System.Diagnostics;
using DataGuard.Core.Interfaces;
using DataGuard.Core.Models;

namespace DataGuard.Core.Rules;

/// <summary>
/// Rule 4: NotNull check for missing or blank values in required columns.
/// </summary>
public sealed class NotNullRule : BaseValidationRule
{
    public NotNullRule(RuleDefinition definition, ISqlExecutor sqlExecutor) 
        : base(definition, sqlExecutor) { }

    public override async Task<RuleResult> ExecuteAsync(RuleContext context, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var cols = Definition.Columns ?? (string.IsNullOrWhiteSpace(Definition.TargetColumn) ? Array.Empty<string>() : new[] { Definition.TargetColumn });
            if (cols.Count == 0)
                throw new InvalidOperationException("At least one column is required for NotNullRule.");

            var allowBlank = Definition.AllowBlank ?? false;
            var (discrepancyCount, badRows) = await SqlExecutor.GetNullOrBlankRowsAsync(
                context.TargetConnectionString,
                Definition.TargetTable,
                cols,
                allowBlank,
                limit: 20,
                Definition.TimeoutSeconds,
                ct);

            sw.Stop();

            if (discrepancyCount == 0)
            {
                return RuleResult.Pass(Definition, Fingerprint, sw.ElapsedMilliseconds, targetVal: "0 nulls");
            }

            return RuleResult.Fail(
                Definition,
                Fingerprint,
                sw.ElapsedMilliseconds,
                discrepancyCount,
                targetVal: $"{discrepancyCount} null or blank rows",
                sampleRows: badRows,
                error: $"Found {discrepancyCount} invalid null/blank rows in table '{Definition.TargetTable}' columns [{string.Join(", ", cols)}]");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return RuleResult.Error(Definition, Fingerprint, sw.ElapsedMilliseconds, ex.Message);
        }
    }
}

/// <summary>
/// Rule 5: DuplicateKey check for candidate or composite key uniqueness.
/// </summary>
public sealed class DuplicateKeyRule : BaseValidationRule
{
    public DuplicateKeyRule(RuleDefinition definition, ISqlExecutor sqlExecutor) 
        : base(definition, sqlExecutor) { }

    public override async Task<RuleResult> ExecuteAsync(RuleContext context, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var keyCols = Definition.KeyColumns ?? Definition.Columns ?? (string.IsNullOrWhiteSpace(Definition.TargetColumn) ? Array.Empty<string>() : new[] { Definition.TargetColumn });
            if (keyCols.Count == 0)
                throw new InvalidOperationException("KeyColumns or TargetColumn is required for DuplicateKeyRule.");

            var (discrepancyCount, badRows) = await SqlExecutor.GetDuplicateKeyRowsAsync(
                context.TargetConnectionString,
                Definition.TargetTable,
                keyCols,
                Definition.FilterCondition,
                limit: 20,
                Definition.TimeoutSeconds,
                ct);

            sw.Stop();

            if (discrepancyCount == 0)
            {
                return RuleResult.Pass(Definition, Fingerprint, sw.ElapsedMilliseconds, targetVal: "0 duplicates");
            }

            return RuleResult.Fail(
                Definition,
                Fingerprint,
                sw.ElapsedMilliseconds,
                discrepancyCount,
                targetVal: $"{discrepancyCount} duplicate key groups",
                sampleRows: badRows,
                error: $"Found {discrepancyCount} duplicate key values in '{Definition.TargetTable}' on key [{string.Join(", ", keyCols)}]");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return RuleResult.Error(Definition, Fingerprint, sw.ElapsedMilliseconds, ex.Message);
        }
    }
}

/// <summary>
/// Rule 6: OrphanKey check for child foreign keys lacking corresponding parent records.
/// </summary>
public sealed class OrphanKeyRule : BaseValidationRule
{
    public OrphanKeyRule(RuleDefinition definition, ISqlExecutor sqlExecutor) 
        : base(definition, sqlExecutor) { }

    public override async Task<RuleResult> ExecuteAsync(RuleContext context, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var childTable = Definition.ChildTable ?? Definition.TargetTable;
            var childCol = Definition.ChildColumn ?? Definition.TargetColumn;
            var parentTable = Definition.ParentTable ?? Definition.SourceTable;
            var parentCol = Definition.ParentColumn ?? Definition.TargetColumn;

            if (string.IsNullOrWhiteSpace(childTable) || string.IsNullOrWhiteSpace(childCol) ||
                string.IsNullOrWhiteSpace(parentTable) || string.IsNullOrWhiteSpace(parentCol))
            {
                throw new InvalidOperationException("ChildTable, ChildColumn, ParentTable, and ParentColumn are required for OrphanKeyRule.");
            }

            var (discrepancyCount, badRows) = await SqlExecutor.GetOrphanKeyRowsAsync(
                context.TargetConnectionString,
                childTable,
                childCol,
                parentTable,
                parentCol,
                limit: 20,
                Definition.TimeoutSeconds,
                ct);

            sw.Stop();

            if (discrepancyCount == 0)
            {
                return RuleResult.Pass(Definition, Fingerprint, sw.ElapsedMilliseconds, targetVal: "0 orphan rows");
            }

            return RuleResult.Fail(
                Definition,
                Fingerprint,
                sw.ElapsedMilliseconds,
                discrepancyCount,
                targetVal: $"{discrepancyCount} orphan rows",
                sampleRows: badRows,
                error: $"Found {discrepancyCount} orphan records in '{childTable}.{childCol}' with no matching '{parentTable}.{parentCol}'");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return RuleResult.Error(Definition, Fingerprint, sw.ElapsedMilliseconds, ex.Message);
        }
    }
}
