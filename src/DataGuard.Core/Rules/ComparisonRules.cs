using System.Diagnostics;
using DataGuard.Core.Enums;
using DataGuard.Core.Interfaces;
using DataGuard.Core.Models;

namespace DataGuard.Core.Rules;

public abstract class BaseValidationRule : IValidationRule
{
    public RuleDefinition Definition { get; }
    protected readonly ISqlExecutor SqlExecutor;
    protected readonly string Fingerprint;

    protected BaseValidationRule(RuleDefinition definition, ISqlExecutor sqlExecutor)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        SqlExecutor = sqlExecutor ?? throw new ArgumentNullException(nameof(sqlExecutor));
        Fingerprint = FingerprintGenerator.Generate(definition.Id, definition.TargetTable, definition.TargetColumn);
    }

    public abstract Task<RuleResult> ExecuteAsync(RuleContext context, CancellationToken ct);
}

/// <summary>
/// Rule 1: RowCount comparison between Source and Target tables.
/// </summary>
public sealed class RowCountRule : BaseValidationRule
{
    public RowCountRule(RuleDefinition definition, ISqlExecutor sqlExecutor) 
        : base(definition, sqlExecutor) { }

    public override async Task<RuleResult> ExecuteAsync(RuleContext context, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var sourceConn = context.SourceConnectionString ?? context.TargetConnectionString;
            var sourceTable = Definition.SourceTable ?? Definition.TargetTable;

            var sourceCountTask = SqlExecutor.GetRowCountAsync(sourceConn, sourceTable, Definition.FilterCondition, Definition.TimeoutSeconds, ct);
            var targetCountTask = SqlExecutor.GetRowCountAsync(context.TargetConnectionString, Definition.TargetTable, Definition.FilterCondition, Definition.TimeoutSeconds, ct);

            await Task.WhenAll(sourceCountTask, targetCountTask);
            sw.Stop();

            var sourceCount = await sourceCountTask;
            var targetCount = await targetCountTask;
            var diff = Math.Abs(sourceCount - targetCount);

            var tolerance = Definition.Tolerance ?? 0m;
            var tolerancePercent = Definition.TolerancePercent ?? 0m;

            var allowedToleranceRows = tolerance > 0 
                ? tolerance 
                : (sourceCount > 0 ? (decimal)sourceCount * (tolerancePercent / 100m) : 0m);

            if (diff <= allowedToleranceRows)
            {
                return RuleResult.Pass(Definition, Fingerprint, sw.ElapsedMilliseconds, sourceCount.ToString(), targetCount.ToString());
            }

            return RuleResult.Fail(
                Definition, 
                Fingerprint, 
                sw.ElapsedMilliseconds, 
                diff, 
                sourceCount.ToString(), 
                targetCount.ToString(),
                error: $"Row count mismatch: Source={sourceCount}, Target={targetCount}, Delta={diff} (Allowed tolerance={allowedToleranceRows})");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return RuleResult.Error(Definition, Fingerprint, sw.ElapsedMilliseconds, ex.Message);
        }
    }
}

/// <summary>
/// Rule 2: Column Aggregate comparison (SUM, MIN, MAX, AVG) with numeric tolerance.
/// </summary>
public sealed class ColumnAggregateRule : BaseValidationRule
{
    public ColumnAggregateRule(RuleDefinition definition, ISqlExecutor sqlExecutor) 
        : base(definition, sqlExecutor) { }

    public override async Task<RuleResult> ExecuteAsync(RuleContext context, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (string.IsNullOrWhiteSpace(Definition.TargetColumn))
                throw new InvalidOperationException("TargetColumn is required for ColumnAggregateRule.");

            var func = Definition.AggregateFunction ?? AggregateFunction.Sum;
            var sourceConn = context.SourceConnectionString ?? context.TargetConnectionString;
            var sourceTable = Definition.SourceTable ?? Definition.TargetTable;

            var sourceValTask = SqlExecutor.GetAggregateAsync(sourceConn, sourceTable, Definition.TargetColumn, func, Definition.FilterCondition, Definition.TimeoutSeconds, ct);
            var targetValTask = SqlExecutor.GetAggregateAsync(context.TargetConnectionString, Definition.TargetTable, Definition.TargetColumn, func, Definition.FilterCondition, Definition.TimeoutSeconds, ct);

            await Task.WhenAll(sourceValTask, targetValTask);
            sw.Stop();

            var sourceVal = await sourceValTask ?? 0m;
            var targetVal = await targetValTask ?? 0m;
            var diff = Math.Abs(sourceVal - targetVal);
            var allowedTolerance = Definition.Tolerance ?? 0m;

            if (diff <= allowedTolerance)
            {
                return RuleResult.Pass(Definition, Fingerprint, sw.ElapsedMilliseconds, sourceVal.ToString("F2"), targetVal.ToString("F2"));
            }

            return RuleResult.Fail(
                Definition,
                Fingerprint,
                sw.ElapsedMilliseconds,
                (long)Math.Ceiling(diff),
                sourceVal.ToString("F2"),
                targetVal.ToString("F2"),
                error: $"Aggregate {func}({Definition.TargetColumn}) mismatch: Source={sourceVal:F2}, Target={targetVal:F2}, Delta={diff:F2} (Tolerance={allowedTolerance:F2})");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return RuleResult.Error(Definition, Fingerprint, sw.ElapsedMilliseconds, ex.Message);
        }
    }
}

/// <summary>
/// Rule 3: Checksum comparison across columns using CHECKSUM_AGG / HASHBYTES.
/// </summary>
public sealed class ChecksumRule : BaseValidationRule
{
    public ChecksumRule(RuleDefinition definition, ISqlExecutor sqlExecutor) 
        : base(definition, sqlExecutor) { }

    public override async Task<RuleResult> ExecuteAsync(RuleContext context, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var cols = Definition.Columns ?? (string.IsNullOrWhiteSpace(Definition.TargetColumn) ? Array.Empty<string>() : new[] { Definition.TargetColumn });
            if (cols.Count == 0)
                throw new InvalidOperationException("At least one column is required for ChecksumRule.");

            var sourceConn = context.SourceConnectionString ?? context.TargetConnectionString;
            var sourceTable = Definition.SourceTable ?? Definition.TargetTable;

            var sourceChecksumTask = SqlExecutor.GetChecksumAggAsync(sourceConn, sourceTable, cols, Definition.FilterCondition, Definition.TimeoutSeconds, ct);
            var targetChecksumTask = SqlExecutor.GetChecksumAggAsync(context.TargetConnectionString, Definition.TargetTable, cols, Definition.FilterCondition, Definition.TimeoutSeconds, ct);

            await Task.WhenAll(sourceChecksumTask, targetChecksumTask);
            sw.Stop();

            var sourceChecksum = await sourceChecksumTask;
            var targetChecksum = await targetChecksumTask;

            if (sourceChecksum == targetChecksum)
            {
                return RuleResult.Pass(Definition, Fingerprint, sw.ElapsedMilliseconds, sourceChecksum.ToString(), targetChecksum.ToString());
            }

            return RuleResult.Fail(
                Definition,
                Fingerprint,
                sw.ElapsedMilliseconds,
                1,
                sourceChecksum.ToString(),
                targetChecksum.ToString(),
                error: $"Checksum hash mismatch: Source={sourceChecksum}, Target={targetChecksum} across columns [{string.Join(", ", cols)}]");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return RuleResult.Error(Definition, Fingerprint, sw.ElapsedMilliseconds, ex.Message);
        }
    }
}
