# Architecture Decision Records (ADR)

## ADR-001: Direct T-SQL & Dapper instead of Entity Framework Core
- **Context**: DataGuard validates high-volume database tables (100k+ to 10M+ rows), schema definitions across databases, and requires fine-grained control over query execution plans, hints, window functions, and `CHECKSUM_AGG`/`HASHBYTES`.
- **Decision**: Use Dapper and `Microsoft.Data.SqlClient` for all metadata and validation operations. No EF Core.
- **Consequences**: Maximum execution performance, deterministic SQL execution plans, zero ORM translation overhead, direct use of T-SQL stored procedures, functions, triggers, and views.

## ADR-002: Zero External Dependencies in DataGuard.Core
- **Context**: The Core domain and rule engine must be strictly testable in isolation and portable.
- **Decision**: `DataGuard.Core` contains only pure C# domain logic, rule strategy definitions, factory interfaces, defect state machines, and contract definitions. Infrastructure details like SqlClient, HTTP clients, and file I/O are restricted to `DataGuard.Infrastructure`.
- **Consequences**: Enables fast in-memory unit tests with mock/fake database providers, clean dependency inversion, and protection against framework lock-in.

## ADR-003: Dynamic SQL Safety via Strict Whitelisting and sp_executesql
- **Context**: Validation rules dynamically construct queries against arbitrary tables and columns specified in rule definitions. Directly concatenating raw strings poses severe SQL injection vulnerabilities.
- **Decision**: Implement a two-phase sanitization architecture:
  1. Whitelist validation: Schema, table, and column names must exist in `INFORMATION_SCHEMA` or `sys.objects`/`sys.columns`.
  2. Formatting with `QUOTENAME`: All verified identifiers are wrapped with `QUOTENAME()`.
  3. Strict parameterization: All filter values, date thresholds, and tolerance values are passed strictly through parameters via `sp_executesql`.
- **Consequences**: Complete immunity to identifier-based and value-based SQL injection attacks, confirmed by dedicated security test suites.

## ADR-004: SHA-256 Defect Fingerprinting & Automated State Lifecycle
- **Context**: Re-running validation against migrating data generates repeated findings. Creating duplicate defect entries pollutes dashboards and obscures remediation progress.
- **Decision**: Generate an invariant SHA-256 fingerprint from `RuleId + TargetTable + TargetColumn`. Failures upsert occurrences on the single fingerprint record. Passing rules automatically close open defects as `Resolved`. Regressions flip `Resolved` back to `Reopened` and increment regression metrics.
- **Consequences**: Clear, de-duplicated tracking with automated lifecycle management.
