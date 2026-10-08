# DataGuard Benchmark Results

*Honesty policy: All metrics below are produced through executed benchmark runs on this host machine.*

## Environment & Machine Specifications
- **Operating System**: Windows (Workstation)
- **CPU**: (Populated during P9 execution)
- **Memory**: (Populated during P9 execution)
- **SQL Server**: SQL Server 2022 Developer Edition (Linux container via Docker Desktop)
- **Data Access**: Dapper v2.1.35 + Microsoft.Data.SqlClient v5.2.2

## Synthetic Dataset Sizing
- **LegacyDB**: 100,000 baseline rows across `Customers`, `Orders`, `OrderItems`, `Invoices`.
- **MigratedDB**: 100,000 migrated rows with deterministic injected anomalies.
- **High-Volume Scale**: 1,000,000 rows across `Customers`, `Orders`, `OrderItems`, `Invoices`.

## Execution Results Table

| Dataset Scale | Rule Suite | Execution Mode | DB Indexing | Total Rules | Duration (ms) | Rules / Sec | Memory Peak (MB) |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| 100k Rows | All 9 Rules | Sequential (`p=1`) | With Indexes | *pending P9* | *pending P9* | *pending P9* | *pending P9* |
| 100k Rows | All 9 Rules | Parallel (`p=4`) | With Indexes | *pending P9* | *pending P9* | *pending P9* | *pending P9* |
| 100k Rows | All 9 Rules | Parallel (`p=4`) | Without Indexes | *pending P9* | *pending P9* | *pending P9* | *pending P9* |
| 1M Rows | All 9 Rules | Parallel (`p=8`) | With Indexes | *pending P9* | *pending P9* | *pending P9* | *pending P9* |

## Index Impact Analysis (Query Plans)
*(Query execution plans before and after index creation on `Orders(CustomerId)`, `OrderItems(OrderId)`, `Customers(Email)` recorded during benchmark execution).*
