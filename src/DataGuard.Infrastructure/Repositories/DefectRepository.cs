using System.Data;
using Dapper;
using DataGuard.Core.Enums;
using DataGuard.Core.Interfaces;
using DataGuard.Core.Models;
using Microsoft.Data.SqlClient;

namespace DataGuard.Infrastructure.Repositories;

public sealed class DefectRepository : IDefectRepository
{
    private readonly string _connectionString;

    public DefectRepository(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<Defect> UpsertDefectAsync(string fingerprint, string ruleId, string targetTable, string? targetColumn, RuleSeverity severity, Guid runId, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        var parameters = new DynamicParameters();
        parameters.Add("Fingerprint", fingerprint);
        parameters.Add("RuleId", ruleId);
        parameters.Add("TargetTable", targetTable);
        parameters.Add("TargetColumn", targetColumn);
        parameters.Add("Severity", severity.ToString());
        parameters.Add("RunId", runId);
        parameters.Add("DefectId", dbType: DbType.Guid, direction: ParameterDirection.Output);

        await conn.ExecuteAsync(new CommandDefinition(
            "dbo.usp_UpsertDefect",
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct)).ConfigureAwait(false);

        var defectId = parameters.Get<Guid>("DefectId");

        const string selectSql = "SELECT * FROM dbo.Defects WHERE DefectId = @DefectId;";
        var record = await conn.QuerySingleAsync<DefectDbRecord>(new CommandDefinition(selectSql, new { DefectId = defectId }, cancellationToken: ct)).ConfigureAwait(false);

        return new Defect
        {
            DefectId = record.DefectId,
            Fingerprint = record.Fingerprint,
            RuleId = record.RuleId,
            TargetTable = record.TargetTable,
            TargetColumn = record.TargetColumn,
            Severity = Enum.Parse<RuleSeverity>(record.Severity),
            Status = Enum.Parse<DefectStatus>(record.Status),
            FirstSeenRunId = record.FirstSeenRunId,
            LastSeenRunId = record.LastSeenRunId,
            OccurrenceCount = record.OccurrenceCount,
            RegressionCount = record.RegressionCount,
            ResolvedRunId = record.ResolvedRunId,
            FirstSeenAtUtc = record.FirstSeenAtUtc,
            LastSeenAtUtc = record.LastSeenAtUtc,
            ResolvedAtUtc = record.ResolvedAtUtc
        };
    }

    public async Task CloseResolvedDefectsAsync(Guid runId, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        await conn.ExecuteAsync(new CommandDefinition(
            "dbo.usp_CloseResolvedDefects",
            new { RunId = runId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Defect>> GetActiveDefectsAsync(CancellationToken ct = default)
    {
        return await GetAllDefectsAsync(status: DefectStatus.Open, severity: null, ct: ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Defect>> GetAllDefectsAsync(DefectStatus? status = null, RuleSeverity? severity = null, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        var sql = "SELECT * FROM dbo.Defects WHERE 1=1";
        var dp = new DynamicParameters();

        if (status.HasValue)
        {
            sql += " AND Status = @Status";
            dp.Add("Status", status.Value.ToString());
        }

        if (severity.HasValue)
        {
            sql += " AND Severity = @Severity";
            dp.Add("Severity", severity.Value.ToString());
        }

        sql += " ORDER BY LastSeenAtUtc DESC;";

        var records = await conn.QueryAsync<DefectDbRecord>(new CommandDefinition(sql, dp, cancellationToken: ct)).ConfigureAwait(false);

        return records.Select(r => new Defect
        {
            DefectId = r.DefectId,
            Fingerprint = r.Fingerprint,
            RuleId = r.RuleId,
            TargetTable = r.TargetTable,
            TargetColumn = r.TargetColumn,
            Severity = Enum.Parse<RuleSeverity>(r.Severity),
            Status = Enum.Parse<DefectStatus>(r.Status),
            FirstSeenRunId = r.FirstSeenRunId,
            LastSeenRunId = r.LastSeenRunId,
            OccurrenceCount = r.OccurrenceCount,
            RegressionCount = r.RegressionCount,
            ResolvedRunId = r.ResolvedRunId,
            FirstSeenAtUtc = r.FirstSeenAtUtc,
            LastSeenAtUtc = r.LastSeenAtUtc,
            ResolvedAtUtc = r.ResolvedAtUtc
        }).ToList();
    }

    private sealed record DefectDbRecord(
        Guid DefectId,
        string Fingerprint,
        string RuleId,
        string TargetTable,
        string? TargetColumn,
        string Severity,
        string Status,
        Guid FirstSeenRunId,
        Guid LastSeenRunId,
        int OccurrenceCount,
        int RegressionCount,
        Guid? ResolvedRunId,
        DateTime FirstSeenAtUtc,
        DateTime LastSeenAtUtc,
        DateTime? ResolvedAtUtc);
}
