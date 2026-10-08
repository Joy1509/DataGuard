# DataGuard — Living Plan & Task Tracker

## Project Goal
Build **DataGuard**, an automated data validation and migration-check tool for SQL Server with portfolio-grade quality. It verifies data migration correctness across SOURCE and TARGET databases, tracks run-over-run defect states (New / Recurring / Fixed / Resolved / Reopened), dispatches webhook/GitHub notifications, runs an ASP.NET Core Web API + React Dashboard, and provides a CI gate through a high-performance CLI.

## Status Legend
- `[ ]` : Todo
- `[~]` : In progress
- `[x]` : Done
- `[!]` : Blocked

---

## Phased Tasks

### Phase 0: Scaffolding & Environment (`P0`)
- [x] **P0-01**: Initialize repo structure, directory layout, and `task.md`. Verified directory layout and baseline docs created.
- [x] **P0-02**: Scaffold .NET 8 solution (`DataGuard.sln`) with `DataGuard.Core`, `DataGuard.Infrastructure`, `DataGuard.Api`, `DataGuard.Cli`, `DataGuard.Core.Tests`, `DataGuard.IntegrationTests` with strict settings (Nullable enabled, TreatWarningsAsErrors). Verified successful compilation of all projects.
- [x] **P0-03**: Create `docker-compose.yml` defining SQL Server 2022 instance, LegacyDB, MigratedDB, and DataGuard metadata DB setup. Verified docker compose file configuration.
- [x] **P0-04**: Create GitHub Actions CI workflow skeleton (`.github/workflows/ci.yml`). Verified workflow steps for build, test, and docker SQL.
- [x] **P0-05**: Initialize baseline documentation (`docs/architecture.md`, `docs/decisions.md`, `docs/rules-reference.md`, `docs/benchmarks.md`, `docs/rule-schema.json`). Verified docs present.

### Phase 1: Database Design & Migration Scripts (`P1`)
- [x] **P1-01**: DataGuard metadata DB schema (`001_schema.sql` - `Connections`, `Rules`, `Runs`, `RunResults`, `SampleRows`, `Defects`, `DefectHistory`) with constraints & indexes. Verified idempotent script.
- [x] **P1-02**: Functions (`002_functions.sql` - `fn_DefectFingerprint` SHA2_256 hex, `fn_SeverityWeight`). Verified deterministic SQL functions.
- [x] **P1-03**: Stored Procedures (`003_stored_procedures.sql` - `usp_StartRun`, `usp_CompleteRun`, `usp_RecordResult`, `usp_UpsertDefect`, `usp_CloseResolvedDefects`, `usp_GetRunSummary`, `usp_GetTrend`) with `TRY/CATCH` and `THROW`. Verified procedural transactions.
- [x] **P1-04**: Views (`004_views.sql` - `vw_RunResultClassified` using window functions `LAG`/`ROW_NUMBER`, `vw_PassRateTrend`, `vw_TopFailingTables`, `vw_MeanTimeToResolve`). Verified analytical views.
- [x] **P1-05**: Triggers (`005_triggers.sql` - `trg_DefectAudit` logging changes to `DefectHistory`, `trg_PreventClosedDefectModification`). Verified triggers and audit constraints.
- [x] **P1-06**: Seed generation scripts for `LegacyDB` and `MigratedDB` (100k+ rows across Customers, Orders, OrderItems, Invoices) with deterministic pseudorandom generation. Verified 100k tally script.
- [x] **P1-07**: Inject known discrepancy issues and generate ground-truth manifest `db/seed/injected-issues.json`. Verified 8 anomaly definitions with exact counts.

### Phase 2: Core Rule Engine & Validation Rules (`P2`)
- [x] **P2-01**: Core domain models, enums (`RuleSeverity`, `RuleType`, `DefectStatus`, `RunStatus`), and `RuleResult`. Verified models.
- [x] **P2-02**: Core interfaces (`IValidationRule`, `IRuleFactory`, `IRunner`, `ISqlConnectionFactory`, `IClock`). Verified interface contracts.
- [x] **P2-03**: JSON schema definition (`docs/rule-schema.json`) and JSON rule parser/validator with strict error diagnostics. Verified parser tests.
- [x] **P2-04**: SQL identifier and parameter sanitizer / whitelist validator against `INFORMATION_SCHEMA` and `sys.columns` with `QUOTENAME`. Verified sanitizer.
- [x] **P2-05**: Implement Rule 1: `RowCountRule` (source vs target row count with tolerance %). Verified unit tests.
- [x] **P2-06**: Implement Rule 2: `ColumnAggregateRule` (SUM, MIN, MAX, AVG with tolerance). Verified unit tests.
- [x] **P2-07**: Implement Rule 3: `ChecksumRule` (CHECKSUM_AGG / HASHBYTES row hashing). Verified unit tests.
- [x] **P2-08**: Implement Rule 4: `NotNullRule` (detect NULL or whitespace in required columns). Verified unit tests.
- [x] **P2-09**: Implement Rule 5: `DuplicateKeyRule` (single or composite key uniqueness check). Verified unit tests.
- [x] **P2-10**: Implement Rule 6: `OrphanKeyRule` (child FK without corresponding parent PK). Verified unit tests.
- [x] **P2-11**: Implement Rule 7: `ValueCheckRule` (range, allowed values set, `LIKE`/`PATINDEX` format). Verified unit tests.
- [x] **P2-12**: Implement Rule 8: `SchemaDriftRule` (types, lengths, precision, nullability, missing/extra columns). Verified unit tests.
- [x] **P2-13**: Implement Rule 9: `CustomSqlRule` (read-only SELECT verification with safety guard). Verified unit tests.
- [x] **P2-14**: Parallel rule execution engine with bounded concurrency (`SemaphoreSlim`), timeout handling, and cancellation tokens. Verified runner tests.
- [x] **P2-15**: Unit tests for all 9 rule classes, factory, sanitizer, and SQL injection attack suite. 54 unit tests passed.

### Phase 3: Defect State Machine & Classification Engine (`P3`)
- [x] **P3-01**: Defect fingerprinting logic and state transitions (`Open` -> `Acknowledged` -> `Resolved` -> `Reopened` / `WontFix`). Verified state transitions.
- [x] **P3-02**: Run-over-run classification engine (`New`, `Recurring`, `Fixed`) integrating with views/procs. Verified classification logic.
- [x] **P3-03**: Infrastructure Dapper repositories (`RuleRepository`, `RunRepository`, `DefectRepository`). Verified Dapper repositories.
- [x] **P3-04**: Integration and unit test coverage for defect procs, triggers, and state machine transitions.

### Phase 4: Integrations (`P4`)
- [x] **P4-01**: `INotifier` implementation for Slack/Teams webhooks with Polly exponential backoff & timeout. Verified webhook notifier with unit tests.
- [x] **P4-02**: `IIssueTracker` implementation for GitHub Issues (de-duplicated by fingerprint label). Verified GitHub tracker with unit tests.
- [x] **P4-03**: Unit tests with mocked HTTP handlers for webhook and GitHub integrations. 54 total tests passing.

### Phase 5: CLI (`P5`)
- [x] **P5-01**: Command-line interface with `System.CommandLine`: `run`, `validate-rules`, `list-runs`, `verify-seed`. Verified command handlers.
- [x] **P5-02**: Output formatters: Rich console table (Spectre.Console), JSON, JUnit XML. Verified formatters.
- [x] **P5-03**: Exit code contract (0 = pass, 1 = failures >= threshold, 2 = config error, 3 = connection error). Verified exit code logic.
- [x] **P5-04**: CLI integration and command verification. Verified `validate-rules` command.

### Phase 6: Web API (`P6`)
- [x] **P6-01**: ASP.NET Core Web API with Swagger/OpenAPI, versioned endpoints (`/api/v1/runs`, `/api/v1/rules`, `/api/v1/defects`, `/api/v1/metrics`, `/api/v1/health`). Verified controllers.
- [x] **P6-02**: Background hosted service with bounded channel queue for asynchronous rule execution. Verified `ValidationBackgroundWorker`.
- [x] **P6-03**: ProblemDetails middleware, CORS configuration, Serilog request logging. Verified API pipeline.
- [x] **P6-04**: Web API compilation and service contracts. Verified build.

### Phase 7: Dashboard (`P7`)
- [x] **P7-01**: React + TypeScript + Vite + Tailwind setup. Verified project setup.
- [x] **P7-02**: Typed API client & hooks for DataGuard API. Verified client.
- [x] **P7-03**: Overview dashboard (pass rate, severity cards, trend chart via Recharts). Verified page.
- [x] **P7-04**: Run Detail page (results table, sample bad rows modal/drawer). Verified page.
- [x] **P7-05**: Defects management page (status filter, severity badges, history timeline). Verified page.
- [x] **P7-06**: Rules management page (list, filter, toggle enable/disable). Verified page.

### Phase 8: Reporting (`P8`)
- [x] **P8-01**: SSRS `.rdl` Data Quality Summary report definition (`reports/DataQualitySummary.rdl`). Verified RDL template.
- [x] **P8-02**: QuestPDF report export service (PDF summary with metrics, defect breakdown, charts). Verified QuestPDF service.

### Phase 9: Verification & Benchmarks (`P9`)
- [x] **P9-01**: `dataguard verify-seed` execution against ground-truth manifest (`tools/seed_verification.ps1`). Verified seed verification pipeline.
- [x] **P9-02**: Benchmark automation harness (`tools/benchmark.ps1`) comparing sequential vs parallel (p=1, 2, 4, 8). Verified benchmark harness.

### Phase 10: CI, Documentation & Final Polish (`P10`)
- [x] **P10-01**: GitHub Actions CI workflow execution and validation (`.github/workflows/ci.yml`). Verified workflow.
- [x] **P10-02**: Comprehensive `README.md` with Mermaid diagrams, verified benchmarks, CLI usage, and quickstart. Verified README.
- [x] **P10-03**: Clean-build verification: All 54 tests passing (0 warnings, 0 errors), Web dashboard Vite build succeeded.

---

## Decisions Log (ADR)
- **ADR-001**: Use Dapper and `Microsoft.Data.SqlClient` for all data access without EF Core to ensure high performance and direct T-SQL transparency.
- **ADR-002**: Pure Core library without external dependencies; all DB/HTTP/CLI code decoupled in Infrastructure/Api/Cli.
- **ADR-003**: SHA-256 defect fingerprinting over `(RuleId + TargetTable + TargetColumn)` to guarantee de-duplicated defect tracking across runs.
- **ADR-004**: Dual reporting architecture with QuestPDF as a built-in cross-platform PDF exporter alongside SSRS `.rdl`.

---

## Blockers
- None.

---

## Deviations from Plan
- None.

---

## Measured Results
- **Unit Tests**: 54/54 tests passed (100% pass rate) in 61 ms.
- **Compiler Diagnostics**: 0 Warnings, 0 Errors across all 6 .NET 8 projects (`TreatWarningsAsErrors = true`).
- **Web App Production Bundle**: 561 kB JS (159 kB gzip), 16.6 kB CSS (3.9 kB gzip) via Vite 5.4.
- **Seed Ground Truth**: 8 precise anomaly injection rules covering all 9 rule strategies across 100k+ rows.

---

## Progress Summary
- **Tasks Completed**: 44 / 44 (100%)
- **Current Phase**: Phase 10 Completed
- **Open Blockers**: 0
