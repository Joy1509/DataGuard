using System.Collections.Concurrent;
using System.Text.Json;
using DataGuard.Core.Enums;
using DataGuard.Core.Interfaces;
using DataGuard.Core.Models;
using DataGuard.Core.Services;

namespace DataGuard.Infrastructure.Repositories;

public sealed class InMemoryDemoRunRepository : IRunRepository
{
    private readonly ConcurrentDictionary<Guid, RunSummary> _runs = new();

    public InMemoryDemoRunRepository()
    {
        // Seed initial demo runs so dashboard has rich charts immediately
        var baseTime = DateTime.UtcNow.AddMinutes(-30);
        for (var i = 1; i <= 5; i++)
        {
            var id = Guid.NewGuid();
            var total = 9;
            var passed = i == 5 ? 7 : (i == 4 ? 6 : (i == 3 ? 5 : (i == 2 ? 4 : 3)));
            var failed = total - passed;
            var duration = 450 + (i * 35);

            _runs[id] = new RunSummary
            {
                RunId = id,
                TriggeredBy = "CLI / Demo",
                Status = RunStatus.Completed,
                TotalRules = total,
                PassedRules = passed,
                FailedRules = failed,
                ErrorRules = 0,
                StartedAtUtc = baseTime.AddMinutes(i * 5),
                CompletedAtUtc = baseTime.AddMinutes(i * 5).AddMilliseconds(duration),
                DurationMs = duration,
                ExitCode = failed > 0 ? 1 : 0,
                SourceConnectionName = "LegacyDB",
                TargetConnectionName = "MigratedDB",
                Results = CreateSampleResults(id)
            };
        }
    }

    public Task<Guid> StartRunAsync(int? sourceConnId, int targetConnId, string triggeredBy, int totalRules, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        _runs[id] = new RunSummary
        {
            RunId = id,
            TriggeredBy = triggeredBy,
            Status = RunStatus.Running,
            TotalRules = totalRules,
            StartedAtUtc = DateTime.UtcNow
        };
        return Task.FromResult(id);
    }

    public Task<Guid> RecordResultAsync(Guid runId, RuleResult result, CancellationToken ct = default)
    {
        return Task.FromResult(Guid.NewGuid());
    }

    public Task CompleteRunAsync(Guid runId, int exitCode, CancellationToken ct = default)
    {
        if (_runs.TryGetValue(runId, out var existing))
        {
            _runs[runId] = existing with
            {
                Status = RunStatus.Completed,
                CompletedAtUtc = DateTime.UtcNow,
                ExitCode = exitCode
            };
        }
        return Task.CompletedTask;
    }

    public Task<RunSummary?> GetSummaryAsync(Guid? runId = null, CancellationToken ct = default)
    {
        if (runId.HasValue && _runs.TryGetValue(runId.Value, out var run))
        {
            return Task.FromResult<RunSummary?>(run);
        }
        var latest = _runs.Values.OrderByDescending(r => r.StartedAtUtc).FirstOrDefault();
        return Task.FromResult(latest);
    }

    public Task<IReadOnlyList<RunSummary>> GetRecentRunsAsync(int top = 30, CancellationToken ct = default)
    {
        var list = _runs.Values.OrderByDescending(r => r.StartedAtUtc).Take(top).ToList();
        return Task.FromResult<IReadOnlyList<RunSummary>>(list);
    }

    public void SaveCompletedRun(RunSummary summary)
    {
        _runs[summary.RunId] = summary;
    }

    private static IReadOnlyList<RuleResult> CreateSampleResults(Guid runId)
    {
        return new List<RuleResult>
        {
            new() { RuleId = "RULE-01-ROWCOUNT", RuleName = "Orders Table Row Count Verification", RuleType = RuleType.RowCount, Severity = RuleSeverity.Critical, TargetTable = "dbo.Orders", Fingerprint = "FP01", Status = RuleStatus.Failed, ExecutionTimeMs = 45, DiscrepancyCount = 15, ErrorMessage = "Row count mismatch: Source=100000, Target=99985, Delta=15" },
            new() { RuleId = "RULE-02-AGGREGATE", RuleName = "Orders TotalAmount Sum Verification", RuleType = RuleType.ColumnAggregate, Severity = RuleSeverity.High, TargetTable = "dbo.Orders", TargetColumn = "TotalAmount", Fingerprint = "FP02", Status = RuleStatus.Failed, ExecutionTimeMs = 38, DiscrepancyCount = 1100, ErrorMessage = "Aggregate Sum(TotalAmount) mismatch: Source=52457820.50, Target=52458920.50, Delta=1100.00" },
            new() { RuleId = "RULE-03-CHECKSUM", RuleName = "OrderItems Product & Pricing Integrity", RuleType = RuleType.Checksum, Severity = RuleSeverity.High, TargetTable = "dbo.OrderItems", Fingerprint = "FP03", Status = RuleStatus.Failed, ExecutionTimeMs = 82, DiscrepancyCount = 1, ErrorMessage = "Checksum hash mismatch across columns [OrderItemId, ProductCode, UnitPrice]" },
            new() { RuleId = "RULE-04-NOTNULL", RuleName = "Customers Mandatory Email Check", RuleType = RuleType.NotNull, Severity = RuleSeverity.High, TargetTable = "dbo.Customers", TargetColumn = "Email", Fingerprint = "FP04", Status = RuleStatus.Failed, ExecutionTimeMs = 28, DiscrepancyCount = 25, SampleRowsJson = new[] { "{\"CustomerId\": 101, \"Email\": null}", "{\"CustomerId\": 102, \"Email\": null}", "{\"CustomerId\": 116, \"Email\": \"   \"}" }, ErrorMessage = "Found 25 invalid null/blank rows in table 'dbo.Customers'" },
            new() { RuleId = "RULE-05-DUPLICATEKEY", RuleName = "Customers Unique CustomerCode Check", RuleType = RuleType.DuplicateKey, Severity = RuleSeverity.Critical, TargetTable = "dbo.Customers", TargetColumn = "CustomerCode", Fingerprint = "FP05", Status = RuleStatus.Failed, ExecutionTimeMs = 32, DiscrepancyCount = 10, SampleRowsJson = new[] { "{\"CustomerCode\": \"CUST-0000001\", \"DuplicateCount\": 11}" }, ErrorMessage = "Found 10 duplicate key values in 'dbo.Customers'" },
            new() { RuleId = "RULE-06-ORPHANKEY", RuleName = "Orders Customer Foreign Key Check", RuleType = RuleType.OrphanKey, Severity = RuleSeverity.Critical, TargetTable = "dbo.Orders", TargetColumn = "CustomerId", Fingerprint = "FP06", Status = RuleStatus.Failed, ExecutionTimeMs = 40, DiscrepancyCount = 20, SampleRowsJson = new[] { "{\"OrderId\": 301, \"CustomerId\": 999999}" }, ErrorMessage = "Found 20 orphan records in 'dbo.Orders.CustomerId'" },
            new() { RuleId = "RULE-07-VALUECHECK", RuleName = "Orders Status Domain Check", RuleType = RuleType.ValueCheck, Severity = RuleSeverity.Medium, TargetTable = "dbo.Orders", TargetColumn = "Status", Fingerprint = "FP07", Status = RuleStatus.Failed, ExecutionTimeMs = 22, DiscrepancyCount = 12, SampleRowsJson = new[] { "{\"OrderId\": 701, \"Status\": \"INVALID_STATUS\"}" }, ErrorMessage = "Found 12 invalid values in 'dbo.Orders.Status'" },
            new() { RuleId = "RULE-08-SCHEMADRIFT", RuleName = "Customers Schema Consistency", RuleType = RuleType.SchemaDrift, Severity = RuleSeverity.Medium, TargetTable = "dbo.Customers", Fingerprint = "FP08", Status = RuleStatus.Failed, ExecutionTimeMs = 18, DiscrepancyCount = 1, SampleRowsJson = new[] { "Column 'Phone' max length mismatch: Source=25, Target=20." }, ErrorMessage = "Schema drift detected: Column 'Phone' max length mismatch: Source=25, Target=20." },
            new() { RuleId = "RULE-09-CUSTOMSQL", RuleName = "Invoices Paid with Zero Amount Due Check", RuleType = RuleType.CustomSql, Severity = RuleSeverity.Low, TargetTable = "dbo.Invoices", Fingerprint = "FP09", Status = RuleStatus.Passed, ExecutionTimeMs = 15, DiscrepancyCount = 0 }
        };
    }
}

public sealed class InMemoryDemoDefectRepository : IDefectRepository
{
    private readonly ConcurrentDictionary<string, Defect> _defects = new();

    public InMemoryDemoDefectRepository()
    {
        var runId = Guid.NewGuid();
        _defects["FP01"] = new Defect { Fingerprint = "FP01", RuleId = "RULE-01-ROWCOUNT", TargetTable = "dbo.Orders", Severity = RuleSeverity.Critical, Status = DefectStatus.Open, FirstSeenRunId = runId, LastSeenRunId = runId, OccurrenceCount = 5, RegressionCount = 0 };
        _defects["FP02"] = new Defect { Fingerprint = "FP02", RuleId = "RULE-02-AGGREGATE", TargetTable = "dbo.Orders", TargetColumn = "TotalAmount", Severity = RuleSeverity.High, Status = DefectStatus.Open, FirstSeenRunId = runId, LastSeenRunId = runId, OccurrenceCount = 5, RegressionCount = 0 };
        _defects["FP04"] = new Defect { Fingerprint = "FP04", RuleId = "RULE-04-NOTNULL", TargetTable = "dbo.Customers", TargetColumn = "Email", Severity = RuleSeverity.High, Status = DefectStatus.Acknowledged, FirstSeenRunId = runId, LastSeenRunId = runId, OccurrenceCount = 4, RegressionCount = 0 };
        _defects["FP05"] = new Defect { Fingerprint = "FP05", RuleId = "RULE-05-DUPLICATEKEY", TargetTable = "dbo.Customers", TargetColumn = "CustomerCode", Severity = RuleSeverity.Critical, Status = DefectStatus.Reopened, FirstSeenRunId = runId, LastSeenRunId = runId, OccurrenceCount = 3, RegressionCount = 1 };
        _defects["FP06"] = new Defect { Fingerprint = "FP06", RuleId = "RULE-06-ORPHANKEY", TargetTable = "dbo.Orders", TargetColumn = "CustomerId", Severity = RuleSeverity.Critical, Status = DefectStatus.Open, FirstSeenRunId = runId, LastSeenRunId = runId, OccurrenceCount = 5, RegressionCount = 0 };
        _defects["FP07"] = new Defect { Fingerprint = "FP07", RuleId = "RULE-07-VALUECHECK", TargetTable = "dbo.Orders", TargetColumn = "Status", Severity = RuleSeverity.Medium, Status = DefectStatus.Resolved, FirstSeenRunId = runId, LastSeenRunId = runId, OccurrenceCount = 2, RegressionCount = 0, ResolvedRunId = runId, ResolvedAtUtc = DateTime.UtcNow.AddMinutes(-10) };
    }

    public Task<Defect> UpsertDefectAsync(string fingerprint, string ruleId, string targetTable, string? targetColumn, RuleSeverity severity, Guid runId, CancellationToken ct = default)
    {
        var defect = _defects.AddOrUpdate(
            fingerprint,
            _ => new Defect { Fingerprint = fingerprint, RuleId = ruleId, TargetTable = targetTable, TargetColumn = targetColumn, Severity = severity, Status = DefectStatus.Open, FirstSeenRunId = runId, LastSeenRunId = runId },
            (_, existing) => existing with { OccurrenceCount = existing.OccurrenceCount + 1, LastSeenRunId = runId, LastSeenAtUtc = DateTime.UtcNow });
        return Task.FromResult(defect);
    }

    public Task CloseResolvedDefectsAsync(Guid runId, CancellationToken ct = default) => Task.CompletedTask;

    public Task<IReadOnlyList<Defect>> GetActiveDefectsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Defect>>(_defects.Values.Where(d => d.Status != DefectStatus.Resolved).ToList());

    public Task<IReadOnlyList<Defect>> GetAllDefectsAsync(DefectStatus? status = null, RuleSeverity? severity = null, CancellationToken ct = default)
    {
        var query = _defects.Values.AsEnumerable();
        if (status.HasValue) query = query.Where(d => d.Status == status.Value);
        if (severity.HasValue) query = query.Where(d => d.Severity == severity.Value);
        return Task.FromResult<IReadOnlyList<Defect>>(query.ToList());
    }
}

public sealed class InMemoryDemoRuleRepository : IRuleRepository
{
    private readonly ConcurrentDictionary<string, RuleDefinition> _rules = new();

    public InMemoryDemoRuleRepository()
    {
        if (File.Exists("rules.sample.json"))
        {
            var (suite, _) = RuleSuiteParser.ParseAndValidate(File.ReadAllText("rules.sample.json"));
            if (suite != null)
            {
                foreach (var r in suite.Rules) _rules[r.Id] = r;
            }
        }
    }

    public Task<IReadOnlyList<RuleDefinition>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<RuleDefinition>>(_rules.Values.ToList());

    public Task<RuleDefinition?> GetByIdAsync(string ruleId, CancellationToken ct = default) =>
        Task.FromResult(_rules.TryGetValue(ruleId, out var r) ? r : null);

    public Task SaveAsync(RuleDefinition rule, CancellationToken ct = default)
    {
        _rules[rule.Id] = rule;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string ruleId, CancellationToken ct = default)
    {
        _rules.TryRemove(ruleId, out _);
        return Task.CompletedTask;
    }
}
