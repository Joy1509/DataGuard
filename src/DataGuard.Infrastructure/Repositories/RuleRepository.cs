using System.Text.Json;
using Dapper;
using DataGuard.Core.Interfaces;
using DataGuard.Core.Models;
using Microsoft.Data.SqlClient;

namespace DataGuard.Infrastructure.Repositories;

public sealed class RuleRepository : IRuleRepository
{
    private readonly string _connectionString;

    public RuleRepository(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<IReadOnlyList<RuleDefinition>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT 
                RuleId AS Id,
                RuleName AS Name,
                Description,
                RuleType AS Type,
                Severity,
                IsEnabled AS Enabled,
                SourceTable,
                TargetTable,
                TargetColumn,
                ConfigurationJson
            FROM dbo.Rules
            ORDER BY RuleId;";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        var rows = await conn.QueryAsync<RuleDbRecord>(new CommandDefinition(sql, cancellationToken: ct)).ConfigureAwait(false);

        var list = new List<RuleDefinition>();
        foreach (var r in rows)
        {
            var def = JsonSerializer.Deserialize<RuleDefinition>(r.ConfigurationJson);
            if (def != null)
            {
                list.Add(def);
            }
        }

        return list;
    }

    public async Task<RuleDefinition?> GetByIdAsync(string ruleId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT ConfigurationJson 
            FROM dbo.Rules 
            WHERE RuleId = @RuleId;";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        var json = await conn.ExecuteScalarAsync<string>(new CommandDefinition(sql, new { RuleId = ruleId }, cancellationToken: ct)).ConfigureAwait(false);
        return json != null ? JsonSerializer.Deserialize<RuleDefinition>(json) : null;
    }

    public async Task SaveAsync(RuleDefinition rule, CancellationToken ct = default)
    {
        const string sql = @"
            MERGE INTO dbo.Rules AS Target
            USING (SELECT @RuleId AS RuleId) AS Source
            ON (Target.RuleId = Source.RuleId)
            WHEN MATCHED THEN
                UPDATE SET 
                    RuleName = @RuleName,
                    Description = @Description,
                    RuleType = @RuleType,
                    Severity = @Severity,
                    IsEnabled = @IsEnabled,
                    SourceTable = @SourceTable,
                    TargetTable = @TargetTable,
                    TargetColumn = @TargetColumn,
                    ConfigurationJson = @ConfigurationJson,
                    UpdatedAtUtc = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (RuleId, RuleName, Description, RuleType, Severity, IsEnabled, SourceTable, TargetTable, TargetColumn, ConfigurationJson, CreatedAtUtc, UpdatedAtUtc)
                VALUES (@RuleId, @RuleName, @Description, @RuleType, @Severity, @IsEnabled, @SourceTable, @TargetTable, @TargetColumn, @ConfigurationJson, SYSUTCDATETIME(), SYSUTCDATETIME());";

        var json = JsonSerializer.Serialize(rule);

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            RuleId = rule.Id,
            RuleName = rule.Name,
            Description = rule.Description,
            RuleType = rule.Type.ToString(),
            Severity = rule.Severity.ToString(),
            IsEnabled = rule.Enabled,
            SourceTable = rule.SourceTable,
            TargetTable = rule.TargetTable,
            TargetColumn = rule.TargetColumn,
            ConfigurationJson = json
        }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string ruleId, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM dbo.Rules WHERE RuleId = @RuleId;";
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(new CommandDefinition(sql, new { RuleId = ruleId }, cancellationToken: ct)).ConfigureAwait(false);
    }

    private sealed record RuleDbRecord(
        string Id,
        string Name,
        string? Description,
        string Type,
        string Severity,
        bool Enabled,
        string? SourceTable,
        string TargetTable,
        string? TargetColumn,
        string ConfigurationJson);
}
