using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;

namespace DataGuard.Infrastructure.Sql;

public interface ISqlSanitizer
{
    string QuoteIdentifier(string identifier);
    string SanitizeTableName(string tableName);
    Task ValidateTableAndColumnsExistAsync(string connectionString, string tableName, IEnumerable<string>? columnNames, CancellationToken ct);
}

public sealed partial class SqlSanitizer : ISqlSanitizer
{
    [GeneratedRegex(@"^[a-zA-Z0-9_]+$", RegexOptions.Compiled)]
    private static partial Regex ValidIdentifierRegex();

    public string QuoteIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentException("Identifier cannot be null or whitespace.", nameof(identifier));

        var clean = identifier.Trim().Trim('[', ']');
        if (!ValidIdentifierRegex().IsMatch(clean))
        {
            throw new ArgumentException($"Invalid SQL identifier '{identifier}'. Only alphanumeric characters and underscores are allowed.", nameof(identifier));
        }

        return $"[{clean}]";
    }

    public string SanitizeTableName(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be null or whitespace.", nameof(tableName));

        var parts = tableName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 1)
        {
            return QuoteIdentifier(parts[0]);
        }
        if (parts.Length == 2)
        {
            return $"{QuoteIdentifier(parts[0])}.{QuoteIdentifier(parts[1])}";
        }
        if (parts.Length == 3)
        {
            return $"{QuoteIdentifier(parts[0])}.{QuoteIdentifier(parts[1])}.{QuoteIdentifier(parts[2])}";
        }

        throw new ArgumentException($"Invalid table specification '{tableName}'.", nameof(tableName));
    }

    public async Task ValidateTableAndColumnsExistAsync(string connectionString, string tableName, IEnumerable<string>? columnNames, CancellationToken ct)
    {
        var parts = tableName.Replace("[", "").Replace("]", "").Split('.');
        var schema = parts.Length > 1 ? parts[0] : "dbo";
        var table = parts.Length > 1 ? parts[1] : parts[0];

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        const string tableCheckSql = @"
            SELECT COUNT(1) 
            FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_SCHEMA = @Schema AND TABLE_NAME = @Table;";

        var tableExists = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(tableCheckSql, new { Schema = schema, Table = table }, cancellationToken: ct)).ConfigureAwait(false);

        if (tableExists == 0)
        {
            throw new InvalidOperationException($"Security validation error: Table '{schema}.{table}' does not exist in target database.");
        }

        if (columnNames != null)
        {
            foreach (var col in columnNames)
            {
                var cleanCol = col.Trim().Trim('[', ']');
                const string colCheckSql = @"
                    SELECT COUNT(1) 
                    FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_SCHEMA = @Schema AND TABLE_NAME = @Table AND COLUMN_NAME = @Column;";

                var colExists = await conn.ExecuteScalarAsync<int>(
                    new CommandDefinition(colCheckSql, new { Schema = schema, Table = table, Column = cleanCol }, cancellationToken: ct)).ConfigureAwait(false);

                if (colExists == 0)
                {
                    throw new InvalidOperationException($"Security validation error: Column '{cleanCol}' does not exist on table '{schema}.{table}'.");
                }
            }
        }
    }
}
