using System.Data;
using System.Text.Json;
using Dapper;
using DataGuard.Core.Enums;
using DataGuard.Core.Interfaces;
using Microsoft.Data.SqlClient;

namespace DataGuard.Infrastructure.Sql;

public sealed class SqlExecutor : ISqlExecutor
{
    private readonly ISqlSanitizer _sanitizer;

    public SqlExecutor(ISqlSanitizer sanitizer)
    {
        _sanitizer = sanitizer ?? throw new ArgumentNullException(nameof(sanitizer));
    }

    public async Task<long> GetRowCountAsync(string connectionString, string tableName, string? filterCondition, int timeoutSeconds, CancellationToken ct)
    {
        var sanitizedTable = _sanitizer.SanitizeTableName(tableName);
        var sql = $"SELECT COUNT_BIG(1) FROM {sanitizedTable}";
        if (!string.IsNullOrWhiteSpace(filterCondition))
        {
            sql += $" WHERE {filterCondition}";
        }

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        var cmd = new CommandDefinition(sql, commandTimeout: timeoutSeconds, cancellationToken: ct);
        return await conn.ExecuteScalarAsync<long>(cmd).ConfigureAwait(false);
    }

    public async Task<decimal?> GetAggregateAsync(string connectionString, string tableName, string columnName, AggregateFunction function, string? filterCondition, int timeoutSeconds, CancellationToken ct)
    {
        var sanitizedTable = _sanitizer.SanitizeTableName(tableName);
        var sanitizedCol = _sanitizer.QuoteIdentifier(columnName);

        var aggFunc = function switch
        {
            AggregateFunction.Sum => "SUM",
            AggregateFunction.Min => "MIN",
            AggregateFunction.Max => "MAX",
            AggregateFunction.Avg => "AVG",
            _ => "SUM"
        };

        var sql = $"SELECT {aggFunc}(CAST({sanitizedCol} AS DECIMAL(38, 4))) FROM {sanitizedTable}";
        if (!string.IsNullOrWhiteSpace(filterCondition))
        {
            sql += $" WHERE {filterCondition}";
        }

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        var cmd = new CommandDefinition(sql, commandTimeout: timeoutSeconds, cancellationToken: ct);
        var result = await conn.ExecuteScalarAsync<decimal?>(cmd).ConfigureAwait(false);
        return result;
    }

    public async Task<long> GetChecksumAggAsync(string connectionString, string tableName, IReadOnlyList<string> columns, string? filterCondition, int timeoutSeconds, CancellationToken ct)
    {
        var sanitizedTable = _sanitizer.SanitizeTableName(tableName);
        var sanitizedCols = columns.Select(c => _sanitizer.QuoteIdentifier(c)).ToList();
        var colsJoined = string.Join(", ", sanitizedCols);

        // CHECKSUM_AGG over binary checksum of target columns
        var sql = $"SELECT ISNULL(CHECKSUM_AGG(BINARY_CHECKSUM({colsJoined})), 0) FROM {sanitizedTable}";
        if (!string.IsNullOrWhiteSpace(filterCondition))
        {
            sql += $" WHERE {filterCondition}";
        }

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        var cmd = new CommandDefinition(sql, commandTimeout: timeoutSeconds, cancellationToken: ct);
        var res = await conn.ExecuteScalarAsync<long>(cmd).ConfigureAwait(false);
        return res;
    }

    public async Task<(long DiscrepancyCount, IReadOnlyList<string> BadRowsJson)> GetNullOrBlankRowsAsync(
        string connectionString, string tableName, IReadOnlyList<string> columns, bool allowBlank, int limit, int timeoutSeconds, CancellationToken ct)
    {
        var sanitizedTable = _sanitizer.SanitizeTableName(tableName);
        var sanitizedCols = columns.Select(c => _sanitizer.QuoteIdentifier(c)).ToList();

        var conditions = new List<string>();
        foreach (var col in sanitizedCols)
        {
            if (allowBlank)
            {
                conditions.Add($"{col} IS NULL");
            }
            else
            {
                conditions.Add($"({col} IS NULL OR LTRIM(RTRIM(CAST({col} AS NVARCHAR(MAX)))) = '')");
            }
        }

        var whereClause = string.Join(" OR ", conditions);

        var countSql = $"SELECT COUNT_BIG(1) FROM {sanitizedTable} WHERE {whereClause}";
        var sampleSql = $"SELECT TOP (@Limit) * FROM {sanitizedTable} WHERE {whereClause}";

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        var count = await conn.ExecuteScalarAsync<long>(
            new CommandDefinition(countSql, commandTimeout: timeoutSeconds, cancellationToken: ct)).ConfigureAwait(false);

        var sampleRows = new List<string>();
        if (count > 0 && limit > 0)
        {
            var rows = await conn.QueryAsync(
                new CommandDefinition(sampleSql, new { Limit = limit }, commandTimeout: timeoutSeconds, cancellationToken: ct)).ConfigureAwait(false);

            foreach (IDictionary<string, object> row in rows)
            {
                sampleRows.Add(JsonSerializer.Serialize(row));
            }
        }

        return (count, sampleRows);
    }

    public async Task<(long DiscrepancyCount, IReadOnlyList<string> BadRowsJson)> GetDuplicateKeyRowsAsync(
        string connectionString, string tableName, IReadOnlyList<string> keyColumns, string? filterCondition, int limit, int timeoutSeconds, CancellationToken ct)
    {
        var sanitizedTable = _sanitizer.SanitizeTableName(tableName);
        var sanitizedKeyCols = keyColumns.Select(c => _sanitizer.QuoteIdentifier(c)).ToList();
        var colsJoined = string.Join(", ", sanitizedKeyCols);

        var whereClause = !string.IsNullOrWhiteSpace(filterCondition) ? $"WHERE {filterCondition}" : "";

        var countSql = $@"
            WITH GroupedKeys AS (
                SELECT {colsJoined}, COUNT(*) AS DuplicateCount
                FROM {sanitizedTable}
                {whereClause}
                GROUP BY {colsJoined}
                HAVING COUNT(*) > 1
            )
            SELECT COUNT_BIG(1) FROM GroupedKeys;";

        var sampleSql = $@"
            WITH GroupedKeys AS (
                SELECT TOP (@Limit) {colsJoined}, COUNT(*) AS DuplicateCount
                FROM {sanitizedTable}
                {whereClause}
                GROUP BY {colsJoined}
                HAVING COUNT(*) > 1
            )
            SELECT * FROM GroupedKeys;";

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        var count = await conn.ExecuteScalarAsync<long>(
            new CommandDefinition(countSql, commandTimeout: timeoutSeconds, cancellationToken: ct)).ConfigureAwait(false);

        var sampleRows = new List<string>();
        if (count > 0 && limit > 0)
        {
            var rows = await conn.QueryAsync(
                new CommandDefinition(sampleSql, new { Limit = limit }, commandTimeout: timeoutSeconds, cancellationToken: ct)).ConfigureAwait(false);

            foreach (IDictionary<string, object> row in rows)
            {
                sampleRows.Add(JsonSerializer.Serialize(row));
            }
        }

        return (count, sampleRows);
    }

    public async Task<(long DiscrepancyCount, IReadOnlyList<string> BadRowsJson)> GetOrphanKeyRowsAsync(
        string connectionString, string childTable, string childColumn, string parentTable, string parentColumn, int limit, int timeoutSeconds, CancellationToken ct)
    {
        var sChildTable = _sanitizer.SanitizeTableName(childTable);
        var sChildCol = _sanitizer.QuoteIdentifier(childColumn);
        var sParentTable = _sanitizer.SanitizeTableName(parentTable);
        var sParentCol = _sanitizer.QuoteIdentifier(parentColumn);

        var countSql = $@"
            SELECT COUNT_BIG(1)
            FROM {sChildTable} c
            WHERE c.{sChildCol} IS NOT NULL
              AND NOT EXISTS (
                  SELECT 1 FROM {sParentTable} p 
                  WHERE p.{sParentCol} = c.{sChildCol}
              );";

        var sampleSql = $@"
            SELECT TOP (@Limit) c.*
            FROM {sChildTable} c
            WHERE c.{sChildCol} IS NOT NULL
              AND NOT EXISTS (
                  SELECT 1 FROM {sParentTable} p 
                  WHERE p.{sParentCol} = c.{sChildCol}
              );";

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        var count = await conn.ExecuteScalarAsync<long>(
            new CommandDefinition(countSql, commandTimeout: timeoutSeconds, cancellationToken: ct)).ConfigureAwait(false);

        var sampleRows = new List<string>();
        if (count > 0 && limit > 0)
        {
            var rows = await conn.QueryAsync(
                new CommandDefinition(sampleSql, new { Limit = limit }, commandTimeout: timeoutSeconds, cancellationToken: ct)).ConfigureAwait(false);

            foreach (IDictionary<string, object> row in rows)
            {
                sampleRows.Add(JsonSerializer.Serialize(row));
            }
        }

        return (count, sampleRows);
    }

    public async Task<(long DiscrepancyCount, IReadOnlyList<string> BadRowsJson)> GetValueCheckRowsAsync(
        string connectionString, string tableName, string columnName, IReadOnlyList<string>? allowedValues, decimal? minVal, decimal? maxVal, string? pattern, int limit, int timeoutSeconds, CancellationToken ct)
    {
        var sanitizedTable = _sanitizer.SanitizeTableName(tableName);
        var sanitizedCol = _sanitizer.QuoteIdentifier(columnName);

        var conditions = new List<string>();
        var dynamicParams = new DynamicParameters();
        dynamicParams.Add("Limit", limit);

        if (allowedValues != null && allowedValues.Count > 0)
        {
            conditions.Add($"{sanitizedCol} NOT IN @AllowedValues");
            dynamicParams.Add("AllowedValues", allowedValues);
        }

        if (minVal.HasValue)
        {
            conditions.Add($"{sanitizedCol} < @MinVal");
            dynamicParams.Add("MinVal", minVal.Value);
        }

        if (maxVal.HasValue)
        {
            conditions.Add($"{sanitizedCol} > @MaxVal");
            dynamicParams.Add("MaxVal", maxVal.Value);
        }

        if (!string.IsNullOrWhiteSpace(pattern))
        {
            conditions.Add($"{sanitizedCol} NOT LIKE @Pattern");
            dynamicParams.Add("Pattern", pattern);
        }

        if (conditions.Count == 0)
        {
            return (0, Array.Empty<string>());
        }

        var whereClause = string.Join(" OR ", conditions);

        var countSql = $"SELECT COUNT_BIG(1) FROM {sanitizedTable} WHERE {whereClause};";
        var sampleSql = $"SELECT TOP (@Limit) * FROM {sanitizedTable} WHERE {whereClause};";

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        var count = await conn.ExecuteScalarAsync<long>(
            new CommandDefinition(countSql, dynamicParams, commandTimeout: timeoutSeconds, cancellationToken: ct)).ConfigureAwait(false);

        var sampleRows = new List<string>();
        if (count > 0 && limit > 0)
        {
            var rows = await conn.QueryAsync(
                new CommandDefinition(sampleSql, dynamicParams, commandTimeout: timeoutSeconds, cancellationToken: ct)).ConfigureAwait(false);

            foreach (IDictionary<string, object> row in rows)
            {
                sampleRows.Add(JsonSerializer.Serialize(row));
            }
        }

        return (count, sampleRows);
    }

    public async Task<(long DiscrepancyCount, IReadOnlyList<string> Discrepancies)> GetSchemaDriftAsync(
        string sourceConn, string sourceTable, string targetConn, string targetTable, int timeoutSeconds, CancellationToken ct)
    {
        var srcParts = sourceTable.Replace("[", "").Replace("]", "").Split('.');
        var srcSchema = srcParts.Length > 1 ? srcParts[0] : "dbo";
        var srcTableName = srcParts.Length > 1 ? srcParts[1] : srcParts[0];

        var tgtParts = targetTable.Replace("[", "").Replace("]", "").Split('.');
        var tgtSchema = tgtParts.Length > 1 ? tgtParts[0] : "dbo";
        var tgtTableName = tgtParts.Length > 1 ? tgtParts[1] : tgtParts[0];

        const string schemaQuery = @"
            SELECT 
                COLUMN_NAME AS ColumnName,
                DATA_TYPE AS DataType,
                IS_NULLABLE AS IsNullable,
                CHARACTER_MAXIMUM_LENGTH AS CharLength,
                NUMERIC_PRECISION AS NumericPrecision,
                NUMERIC_SCALE AS NumericScale
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = @Schema AND TABLE_NAME = @Table;";

        await using var connSrc = new SqlConnection(sourceConn);
        await connSrc.OpenAsync(ct).ConfigureAwait(false);
        var srcCols = (await connSrc.QueryAsync<ColumnSchemaInfo>(
            new CommandDefinition(schemaQuery, new { Schema = srcSchema, Table = srcTableName }, commandTimeout: timeoutSeconds, cancellationToken: ct)).ConfigureAwait(false))
            .ToDictionary(c => c.ColumnName, StringComparer.OrdinalIgnoreCase);

        await using var connTgt = new SqlConnection(targetConn);
        await connTgt.OpenAsync(ct).ConfigureAwait(false);
        var tgtCols = (await connTgt.QueryAsync<ColumnSchemaInfo>(
            new CommandDefinition(schemaQuery, new { Schema = tgtSchema, Table = tgtTableName }, commandTimeout: timeoutSeconds, cancellationToken: ct)).ConfigureAwait(false))
            .ToDictionary(c => c.ColumnName, StringComparer.OrdinalIgnoreCase);

        var discrepancies = new List<string>();

        // Missing columns in Target
        foreach (var (colName, srcInfo) in srcCols)
        {
            if (!tgtCols.TryGetValue(colName, out var tgtInfo))
            {
                discrepancies.Add($"Column '{colName}' exists in source but is MISSING in target.");
                continue;
            }

            if (!string.Equals(srcInfo.DataType, tgtInfo.DataType, StringComparison.OrdinalIgnoreCase))
            {
                discrepancies.Add($"Column '{colName}' data type mismatch: Source={srcInfo.DataType}, Target={tgtInfo.DataType}.");
            }

            if (srcInfo.CharLength != tgtInfo.CharLength && srcInfo.CharLength.HasValue && tgtInfo.CharLength.HasValue)
            {
                discrepancies.Add($"Column '{colName}' max length mismatch: Source={srcInfo.CharLength}, Target={tgtInfo.CharLength}.");
            }

            if (!string.Equals(srcInfo.IsNullable, tgtInfo.IsNullable, StringComparison.OrdinalIgnoreCase))
            {
                discrepancies.Add($"Column '{colName}' nullability mismatch: Source={srcInfo.IsNullable}, Target={tgtInfo.IsNullable}.");
            }
        }

        // Extra columns in Target
        foreach (var (colName, _) in tgtCols)
        {
            if (!srcCols.ContainsKey(colName))
            {
                discrepancies.Add($"Column '{colName}' exists in target but is EXTRA (missing in source).");
            }
        }

        return (discrepancies.Count, discrepancies);
    }

    public async Task<(long ResultCount, IReadOnlyList<string> BadRowsJson)> ExecuteCustomSqlAsync(
        string connectionString, string selectSql, int limit, int timeoutSeconds, CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        var rows = (await conn.QueryAsync(
            new CommandDefinition(selectSql, commandTimeout: timeoutSeconds, cancellationToken: ct)).ConfigureAwait(false)).ToList();

        var sampleRows = new List<string>();
        foreach (IDictionary<string, object> row in rows.Take(limit))
        {
            sampleRows.Add(JsonSerializer.Serialize(row));
        }

        return (rows.Count, sampleRows);
    }

    private sealed record ColumnSchemaInfo(
        string ColumnName,
        string DataType,
        string IsNullable,
        int? CharLength,
        int? NumericPrecision,
        int? NumericScale);
}
