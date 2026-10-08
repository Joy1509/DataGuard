using System.Diagnostics;
using DataGuard.Core.Interfaces;
using DataGuard.Core.Models;

namespace DataGuard.Core.Rules;

/// <summary>
/// Rule 7: ValueCheck for allowed sets, min/max ranges, or PATINDEX patterns.
/// </summary>
public sealed class ValueCheckRule : BaseValidationRule
{
    public ValueCheckRule(RuleDefinition definition, ISqlExecutor sqlExecutor) 
        : base(definition, sqlExecutor) { }

    public override async Task<RuleResult> ExecuteAsync(RuleContext context, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (string.IsNullOrWhiteSpace(Definition.TargetColumn))
                throw new InvalidOperationException("TargetColumn is required for ValueCheckRule.");

            var (discrepancyCount, badRows) = await SqlExecutor.GetValueCheckRowsAsync(
                context.TargetConnectionString,
                Definition.TargetTable,
                Definition.TargetColumn,
                Definition.AllowedValues,
                Definition.MinValue,
                Definition.MaxValue,
                Definition.Pattern,
                limit: 20,
                Definition.TimeoutSeconds,
                ct);

            sw.Stop();

            if (discrepancyCount == 0)
            {
                return RuleResult.Pass(Definition, Fingerprint, sw.ElapsedMilliseconds, targetVal: "0 invalid values");
            }

            return RuleResult.Fail(
                Definition,
                Fingerprint,
                sw.ElapsedMilliseconds,
                discrepancyCount,
                targetVal: $"{discrepancyCount} invalid values",
                sampleRows: badRows,
                error: $"Found {discrepancyCount} invalid values in '{Definition.TargetTable}.{Definition.TargetColumn}' violating bounds/patterns/allowed set.");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return RuleResult.Error(Definition, Fingerprint, sw.ElapsedMilliseconds, ex.Message);
        }
    }
}

/// <summary>
/// Rule 8: SchemaDrift for detecting mismatched column types, lengths, nullability, and missing columns.
/// </summary>
public sealed class SchemaDriftRule : BaseValidationRule
{
    public SchemaDriftRule(RuleDefinition definition, ISqlExecutor sqlExecutor) 
        : base(definition, sqlExecutor) { }

    public override async Task<RuleResult> ExecuteAsync(RuleContext context, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var sourceConn = context.SourceConnectionString ?? context.TargetConnectionString;
            var sourceTable = Definition.SourceTable ?? Definition.TargetTable;

            var (driftCount, driftDetails) = await SqlExecutor.GetSchemaDriftAsync(
                sourceConn,
                sourceTable,
                context.TargetConnectionString,
                Definition.TargetTable,
                Definition.TimeoutSeconds,
                ct);

            sw.Stop();

            if (driftCount == 0)
            {
                return RuleResult.Pass(Definition, Fingerprint, sw.ElapsedMilliseconds, "Schema match", "Schema match");
            }

            return RuleResult.Fail(
                Definition,
                Fingerprint,
                sw.ElapsedMilliseconds,
                driftCount,
                sourceVal: $"{driftCount} schema discrepancies",
                targetVal: "Drift detected",
                sampleRows: driftDetails,
                error: $"Schema drift detected between '{sourceTable}' and '{Definition.TargetTable}': {string.Join("; ", driftDetails)}");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return RuleResult.Error(Definition, Fingerprint, sw.ElapsedMilliseconds, ex.Message);
        }
    }
}

/// <summary>
/// Rule 9: CustomSql for executing user-defined read-only validation queries.
/// </summary>
public sealed class CustomSqlRule : BaseValidationRule
{
    public CustomSqlRule(RuleDefinition definition, ISqlExecutor sqlExecutor) 
        : base(definition, sqlExecutor) { }

    public override async Task<RuleResult> ExecuteAsync(RuleContext context, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (string.IsNullOrWhiteSpace(Definition.CustomSql))
                throw new InvalidOperationException("CustomSql is required for CustomSqlRule.");

            // Guard: Disallow non-SELECT statements
            var trimmed = Definition.CustomSql.Trim();
            if (!trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) &&
                !trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("CustomSql queries must be SELECT-only statements.");
            }

            var expected = Definition.ExpectedCount ?? 0;
            var (actualCount, badRows) = await SqlExecutor.ExecuteCustomSqlAsync(
                context.TargetConnectionString,
                Definition.CustomSql,
                limit: 20,
                Definition.TimeoutSeconds,
                ct);

            sw.Stop();

            if (actualCount == expected)
            {
                return RuleResult.Pass(Definition, Fingerprint, sw.ElapsedMilliseconds, expected.ToString(), actualCount.ToString());
            }

            var delta = Math.Abs(actualCount - expected);
            return RuleResult.Fail(
                Definition,
                Fingerprint,
                sw.ElapsedMilliseconds,
                delta,
                expected.ToString(),
                actualCount.ToString(),
                sampleRows: badRows,
                error: $"Custom SQL violation: Expected {expected} rows, but query returned {actualCount} rows.");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return RuleResult.Error(Definition, Fingerprint, sw.ElapsedMilliseconds, ex.Message);
        }
    }
}
