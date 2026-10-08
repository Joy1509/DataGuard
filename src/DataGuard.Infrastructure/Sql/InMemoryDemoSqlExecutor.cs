using System.Text.Json;
using DataGuard.Core.Enums;
using DataGuard.Core.Interfaces;

namespace DataGuard.Infrastructure.Sql;

/// <summary>
/// In-memory SQL Executor simulating LegacyDB and MigratedDB with ground-truth discrepancies
/// for offline demos and local showcases without an active SQL Server database.
/// </summary>
public sealed class InMemoryDemoSqlExecutor : ISqlExecutor
{
    public Task<long> GetRowCountAsync(string connectionString, string tableName, string? filterCondition, int timeoutSeconds, CancellationToken ct)
    {
        var clean = tableName.ToLowerInvariant();
        if (clean.Contains("order"))
        {
            // MigratedDB has 15 missing orders (99,985 vs 100,000)
            return Task.FromResult(connectionString.Contains("MigratedDB", StringComparison.OrdinalIgnoreCase) ? 99985L : 100000L);
        }
        if (clean.Contains("customer"))
        {
            return Task.FromResult(100000L);
        }
        if (clean.Contains("orderitem"))
        {
            return Task.FromResult(200000L);
        }
        if (clean.Contains("invoice"))
        {
            return Task.FromResult(100000L);
        }
        return Task.FromResult(50000L);
    }

    public Task<decimal?> GetAggregateAsync(string connectionString, string tableName, string columnName, AggregateFunction function, string? filterCondition, int timeoutSeconds, CancellationToken ct)
    {
        // MigratedDB has $1,100 discrepancy in TotalAmount
        if (columnName.Equals("TotalAmount", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<decimal?>(connectionString.Contains("MigratedDB", StringComparison.OrdinalIgnoreCase) ? 52458920.50m : 52457820.50m);
        }
        return Task.FromResult<decimal?>(1000000m);
    }

    public Task<long> GetChecksumAggAsync(string connectionString, string tableName, IReadOnlyList<string> columns, string? filterCondition, int timeoutSeconds, CancellationToken ct)
    {
        // Checksum mismatch on OrderItems (18 corrupted rows)
        if (connectionString.Contains("MigratedDB", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(87654321L);
        }
        return Task.FromResult(12345678L);
    }

    public Task<(long DiscrepancyCount, IReadOnlyList<string> BadRowsJson)> GetNullOrBlankRowsAsync(string connectionString, string tableName, IReadOnlyList<string> columns, bool allowBlank, int limit, int timeoutSeconds, CancellationToken ct)
    {
        // 25 customers with NULL or whitespace Email
        var sampleRows = new List<string>
        {
            JsonSerializer.Serialize(new { CustomerId = 101, CustomerCode = "CUST-0000101", FirstName = "First_101", LastName = "Last_101", Email = (string?)null, Status = "Active" }),
            JsonSerializer.Serialize(new { CustomerId = 102, CustomerCode = "CUST-0000102", FirstName = "First_102", LastName = "Last_102", Email = (string?)null, Status = "Active" }),
            JsonSerializer.Serialize(new { CustomerId = 116, CustomerCode = "CUST-0000116", FirstName = "First_116", LastName = "Last_116", Email = "   ", Status = "Inactive" })
        };
        return Task.FromResult((25L, (IReadOnlyList<string>)sampleRows));
    }

    public Task<(long DiscrepancyCount, IReadOnlyList<string> BadRowsJson)> GetDuplicateKeyRowsAsync(string connectionString, string tableName, IReadOnlyList<string> keyColumns, string? filterCondition, int limit, int timeoutSeconds, CancellationToken ct)
    {
        // 10 duplicate CustomerCode values
        var sampleRows = new List<string>
        {
            JsonSerializer.Serialize(new { CustomerCode = "CUST-0000001", DuplicateCount = 11, AffectedCustomerIds = new[] { 1, 201, 202, 203, 204, 205, 206, 207, 208, 209, 210 } })
        };
        return Task.FromResult((10L, (IReadOnlyList<string>)sampleRows));
    }

    public Task<(long DiscrepancyCount, IReadOnlyList<string> BadRowsJson)> GetOrphanKeyRowsAsync(string connectionString, string childTable, string childColumn, string parentTable, string parentColumn, int limit, int timeoutSeconds, CancellationToken ct)
    {
        // 20 orphan Orders referencing non-existent CustomerId 999999
        var sampleRows = new List<string>
        {
            JsonSerializer.Serialize(new { OrderId = 301, CustomerId = 999999, OrderDate = "2021-04-12T10:15:00", TotalAmount = 450.00, Status = "Processing" }),
            JsonSerializer.Serialize(new { OrderId = 302, CustomerId = 999999, OrderDate = "2021-04-12T11:20:00", TotalAmount = 890.00, Status = "Shipped" })
        };
        return Task.FromResult((20L, (IReadOnlyList<string>)sampleRows));
    }

    public Task<(long DiscrepancyCount, IReadOnlyList<string> BadRowsJson)> GetValueCheckRowsAsync(string connectionString, string tableName, string columnName, IReadOnlyList<string>? allowedValues, decimal? minVal, decimal? maxVal, string? pattern, int limit, int timeoutSeconds, CancellationToken ct)
    {
        // 12 orders with illegal status 'INVALID_STATUS'
        var sampleRows = new List<string>
        {
            JsonSerializer.Serialize(new { OrderId = 701, CustomerId = 701, Status = "INVALID_STATUS", TotalAmount = 210.00 }),
            JsonSerializer.Serialize(new { OrderId = 702, CustomerId = 702, Status = "INVALID_STATUS", TotalAmount = 150.00 })
        };
        return Task.FromResult((12L, (IReadOnlyList<string>)sampleRows));
    }

    public Task<(long DiscrepancyCount, IReadOnlyList<string> Discrepancies)> GetSchemaDriftAsync(string sourceConn, string sourceTable, string targetConn, string targetTable, int timeoutSeconds, CancellationToken ct)
    {
        var discrepancies = new List<string>
        {
            "Column 'Phone' max length mismatch: Source=25 (VARCHAR), Target=20 (VARCHAR)."
        };
        return Task.FromResult((1L, (IReadOnlyList<string>)discrepancies));
    }

    public Task<(long ResultCount, IReadOnlyList<string> BadRowsJson)> ExecuteCustomSqlAsync(string connectionString, string selectSql, int limit, int timeoutSeconds, CancellationToken ct)
    {
        return Task.FromResult((0L, (IReadOnlyList<string>)Array.Empty<string>()));
    }
}
