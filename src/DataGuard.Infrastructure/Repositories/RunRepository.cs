using System.Data;
using System.Text.Json;
using Dapper;
using DataGuard.Core.Enums;
using DataGuard.Core.Interfaces;
using DataGuard.Core.Models;
using Microsoft.Data.SqlClient;

namespace DataGuard.Infrastructure.Repositories;

public sealed class RunRepository : IRunRepository
{
    private readonly string _connectionString;

    public RunRepository(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<Guid> StartRunAsync(int? sourceConnId, int targetConnId, string triggeredBy, int totalRules, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        var parameters = new DynamicParameters();
        parameters.Add("SourceConnectionId", sourceConnId);
        parameters.Add("TargetConnectionId", targetConnId);
        parameters.Add("TriggeredBy", triggeredBy);
        parameters.Add("TotalRules", totalRules);
        parameters.Add("RunId", dbType: DbType.Guid, direction: ParameterDirection.Output);

        await conn.ExecuteAsync(new CommandDefinition(
            "dbo.usp_StartRun",
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct)).ConfigureAwait(false);

        return parameters.Get<Guid>("RunId");
    }

    public async Task<Guid> RecordResultAsync(Guid runId, RuleResult result, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        var sampleJson = result.SampleRowsJson.Count > 0 ? JsonSerializer.Serialize(result.SampleRowsJson) : null;

        var parameters = new DynamicParameters();
        parameters.Add("RunId", runId);
        parameters.Add("RuleId", result.RuleId);
        parameters.Add("Status", result.Status.ToString());
        parameters.Add("ExecutionTimeMs", result.ExecutionTimeMs);
        parameters.Add("SourceValue", result.SourceValue);
        parameters.Add("TargetValue", result.TargetValue);
        parameters.Add("DiscrepancyCount", result.DiscrepancyCount);
        parameters.Add("ErrorMessage", result.ErrorMessage);
        parameters.Add("Fingerprint", result.Fingerprint);
        parameters.Add("SampleRowsJson", sampleJson);
        parameters.Add("ResultId", dbType: DbType.Guid, direction: ParameterDirection.Output);

        await conn.ExecuteAsync(new CommandDefinition(
            "dbo.usp_RecordResult",
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct)).ConfigureAwait(false);

        return parameters.Get<Guid>("ResultId");
    }

    public async Task CompleteRunAsync(Guid runId, int exitCode, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        await conn.ExecuteAsync(new CommandDefinition(
            "dbo.usp_CompleteRun",
            new { RunId = runId, ExitCode = exitCode },
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<RunSummary?> GetSummaryAsync(Guid? runId = null, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        using var multi = await conn.QueryMultipleAsync(new CommandDefinition(
            "dbo.usp_GetRunSummary",
            new { RunId = runId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct)).ConfigureAwait(false);

        var header = await multi.ReadFirstOrDefaultAsync<RunHeaderRecord>().ConfigureAwait(false);
        if (header == null) return null;

        var resultsRecords = (await multi.ReadAsync<RunResultRecord>().ConfigureAwait(false)).ToList();

        var results = resultsRecords.Select(r => new RuleResult
        {
            RuleId = r.RuleId,
            RuleName = r.RuleName,
            RuleType = Enum.Parse<RuleType>(r.RuleType),
            Severity = Enum.Parse<RuleSeverity>(r.Severity),
            TargetTable = r.TargetTable,
            TargetColumn = r.TargetColumn,
            Fingerprint = r.Fingerprint,
            Status = Enum.Parse<RuleStatus>(r.Status),
            ExecutionTimeMs = r.ExecutionTimeMs,
            DiscrepancyCount = r.DiscrepancyCount,
            ErrorMessage = r.ErrorMessage
        }).ToList();

        return new RunSummary
        {
            RunId = header.RunId,
            TriggeredBy = header.TriggeredBy ?? "System",
            Status = Enum.Parse<RunStatus>(header.Status),
            TotalRules = header.TotalRules,
            PassedRules = header.PassedRules,
            FailedRules = header.FailedRules,
            ErrorRules = header.ErrorRules,
            StartedAtUtc = header.StartedAtUtc,
            CompletedAtUtc = header.CompletedAtUtc,
            DurationMs = header.DurationMs,
            ExitCode = header.ExitCode,
            SourceConnectionName = header.SourceConnectionName,
            TargetConnectionName = header.TargetConnectionName,
            Results = results
        };
    }

    public async Task<IReadOnlyList<RunSummary>> GetRecentRunsAsync(int top = 30, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        var rows = await conn.QueryAsync<RunHeaderRecord>(new CommandDefinition(
            "dbo.usp_GetTrend",
            new { TopRuns = top },
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct)).ConfigureAwait(false);

        return rows.Select(h => new RunSummary
        {
            RunId = h.RunId,
            TriggeredBy = h.TriggeredBy ?? "System",
            Status = Enum.TryParse<RunStatus>(h.Status, out var s) ? s : RunStatus.Completed,
            TotalRules = h.TotalRules,
            PassedRules = h.PassedRules,
            FailedRules = h.FailedRules,
            ErrorRules = h.ErrorRules,
            StartedAtUtc = h.StartedAtUtc,
            CompletedAtUtc = h.CompletedAtUtc,
            DurationMs = h.DurationMs,
            ExitCode = h.ExitCode
        }).ToList();
    }

    private sealed record RunHeaderRecord(
        Guid RunId,
        string? TriggeredBy,
        string Status,
        int TotalRules,
        int PassedRules,
        int FailedRules,
        int ErrorRules,
        decimal PassRatePercent,
        DateTime StartedAtUtc,
        DateTime? CompletedAtUtc,
        long? DurationMs,
        int? ExitCode,
        string? SourceConnectionName,
        string? TargetConnectionName);

    private sealed record RunResultRecord(
        Guid ResultId,
        string RuleId,
        string RuleName,
        string RuleType,
        string Severity,
        string TargetTable,
        string? TargetColumn,
        string Status,
        long DiscrepancyCount,
        long ExecutionTimeMs,
        string? ErrorMessage,
        string Fingerprint);
}
