# DataGuard — Command Reference & Operations Guide

A complete reference guide detailing all commands for **DataGuard**, their operational requirements, business purpose, and execution context.

---

## Table of Contents
1. [Quick-Start Showcase (3-Step Demo)](#1-quick-start-showcase-3-step-demo)
2. [CLI Commands (System.CommandLine)](#2-cli-commands-systemcommandline)
3. [Testing, Quality Assurance & Build](#3-testing-quality-assurance--build)
4. [React Web Dashboard Operations](#4-react-web-dashboard-operations)
5. [Docker & SQL Server 2022 Management](#5-docker--sql-server-2022-management)
6. [Benchmarking & Verification Tools](#6-benchmarking--verification-tools)
7. [Process Management & Troubleshooting](#7-process-management--troubleshooting)
8. [Interactive URLs & Endpoints](#8-interactive-urls--endpoints)

---

## 1. Quick-Start Showcase (3-Step Demo)

Use this 3-terminal workflow to present the full end-to-end architecture (Backend API, UI Dashboard, and CLI Tool) to clients, interviewers, or team members.

### Step 1: Start Backend Web API
```powershell
.\run-api.ps1
```
- **Why It Is Required:** The backend API hosts the rule engine, defect tracker, persistence repositories, and WebSocket/REST polling endpoints for the frontend dashboard.
- **Purpose:** Starts the ASP.NET Core 8 Web API on `http://localhost:5000` with Swagger OpenAPI documentation. If a local SQL Server is not detected, it automatically activates the built-in in-memory demo engine with realistic migration data.

### Step 2: Start React Web Dashboard
```powershell
.\run-dashboard.ps1
```
*(Alternative: `cd web; npm run dev`)*
- **Why It Is Required:** Provides a modern UI to visualize runs, track defects, inspect failing SQL rows, and trigger runs interactively.
- **Purpose:** Starts Vite development server at `http://localhost:5173` with fast hot-module replacement (HMR) and connects to the API proxy.

### Step 3: Run Validation via Terminal (CLI)
```powershell
.\run-cli.ps1
```
- **Why It Is Required:** Demonstrates standalone automation capability that can be plugged into CI/CD pipelines without needing a browser.
- **Purpose:** Executes all 9 validation rule strategies, fingerprints defects with SHA-256 hashes, displays rich ASCII tables with ANSI colored pass/fail statuses, and returns exit code `0` (pass) or `1` (fail).

---

## 2. CLI Commands (`System.CommandLine`)

All CLI commands can be invoked using `dotnet run --project src/DataGuard.Cli/DataGuard.Cli.csproj -- <command>` or via the shortcut script `.\run-cli.ps1`.

### 2.1. Run Validation in Demo Mode
```powershell
dotnet run --project src/DataGuard.Cli/DataGuard.Cli.csproj -- run --demo --format table
```
- **Why It Is Required:** Allows you to test or demonstrate DataGuard on any machine (e.g. laptop or CI agent) without configuring a local SQL Server instance.
- **Purpose:** Runs sample migration rules against the embedded validation engine and displays a styled console summary table.

---

### 2.2. Run Validation Against Live SQL Server
```powershell
dotnet run --project src/DataGuard.Cli/DataGuard.Cli.csproj -- run `
  --rules rules.sample.json `
  --source "Server=localhost;Database=LegacyDB;TrustServerCertificate=True;" `
  --target "Server=localhost;Database=MigratedDB;TrustServerCertificate=True;" `
  --format table `
  --parallelism 4
```
- **Why It Is Required:** Used during actual database migration projects to validate live data between source and target databases.
- **Purpose:**
  - Connects to the source and target SQL Server databases.
  - Executes validation rules concurrently using a thread pool of 4 parallel workers.
  - Queries sample bad rows and classifies defects as `New`, `Recurring`, or `Fixed`.

---

### 2.3. Output Results as Machine-Readable JSON
```powershell
dotnet run --project src/DataGuard.Cli/DataGuard.Cli.csproj -- run --demo --format json
```
- **Why It Is Required:** Needed when piping validation results into other automation scripts, webhook dispatchers, or custom logging pipelines.
- **Purpose:** Outputs full execution telemetry, execution timings per rule, defect lists, and sample rows as structured JSON.

---

### 2.4. Output JUnit XML (For CI/CD Pipeline Gating)
```powershell
dotnet run --project src/DataGuard.Cli/DataGuard.Cli.csproj -- run --demo --format junit
```
- **Why It Is Required:** CI/CD orchestrators (GitHub Actions, Azure DevOps, Jenkins, GitLab CI) require standard JUnit XML format to generate test report tabs and mark pull requests as passed or failed.
- **Purpose:** Emits standard `<testsuite>` and `<testcase>` XML tags mapping each validation rule to an automated test case with stack traces and defect descriptions.

---

### 2.5. Validate Rule JSON Schema & Syntax
```powershell
dotnet run --project src/DataGuard.Cli/DataGuard.Cli.csproj -- validate-rules --file rules.sample.json
```
- **Why It Is Required:** Prevents pipeline runtime errors caused by typos, invalid SQL syntax, or missing JSON fields before starting heavy database queries.
- **Purpose:** Parses and validates the rule definition JSON against DataGuard's schema rules, verifying that mandatory fields (RuleType, Severity, Thresholds, SQL Expressions) are valid.

---

### 2.6. Verify Ground-Truth Seed Manifest
```powershell
dotnet run --project src/DataGuard.Cli/DataGuard.Cli.csproj -- verify-seed `
  --manifest db/seed/injected-issues.json `
  --rules rules.sample.json
```
- **Why It Is Required:** Ensures test fixture integrity by verifying that intentional data defects seeded in the database are 100% accurately detected by the rule suite (zero false negatives and zero false positives).
- **Purpose:** Compares the actual detected defects against the expected manifest of 6 injected issues and reports verification precision.

---

## 3. Testing, Quality Assurance & Build

### 3.1. Run Full Unit Test Suite
```powershell
dotnet test DataGuard.sln
```
- **Why It Is Required:** Verifies system correctness across all core modules (SQL generation, hash fingerprinting, defect lifecycle state machine, validation engine).
- **Purpose:** Executes all 54 xUnit unit tests across the solution and ensures no regressions were introduced.

---

### 3.2. Run Core Tests with Verbose Output
```powershell
dotnet test tests/DataGuard.Core.Tests/DataGuard.Core.Tests.csproj --logger "console;verbosity=normal"
```
- **Why It Is Required:** Used during development or debugging to inspect the exact name and duration of each passing/failing test case.
- **Purpose:** Runs the domain core tests with verbose test runner logging.

---

### 3.3. Build Solution with Warnings-As-Errors
```powershell
dotnet build DataGuard.sln
```
- **Why It Is Required:** Ensures enterprise code quality and zero compiler warnings before merging code or generating releases.
- **Purpose:** Compiles all C# projects with `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.

---

## 4. React Web Dashboard Operations

### 4.1. Install Dashboard Dependencies
```powershell
cd web
npm install
```
- **Why It Is Required:** Installs frontend dependencies (React 18, Vite, Lucide icons, Recharts, TailwindCSS).
- **Purpose:** Prepares the `node_modules/` folder before launching the web UI.

---

### 4.2. Start Vite Dev Server
```powershell
cd web
npm run dev
```
- **Why It Is Required:** Runs the local frontend development server with hot-reloading.
- **Purpose:** Hosts the UI at `http://localhost:5173` and proxies `/api/*` network calls to `http://localhost:5000`.

---

### 4.3. Compile Production UI Build
```powershell
cd web
npm run build
```
- **Why It Is Required:** Generates optimized static HTML/JS/CSS assets for production deployment or CDN hosting.
- **Purpose:** Bundles and minifies the React application into the `web/dist/` directory.

---

## 5. Docker & SQL Server 2022 Management

### 5.1. Start SQL Server 2022 Container
```powershell
docker compose up -d
```
- **Why It Is Required:** Provides a local SQL Server instance with the `LegacyDB` and `MigratedDB` schemas initialized.
- **Purpose:** Downloads and starts the official Microsoft SQL Server 2022 container in detached background mode on port `1433`.

---

### 5.2. Check SQL Server Container Status
```powershell
docker compose ps
```
- **Why It Is Required:** Confirms if the database container is healthy and accepting connections.
- **Purpose:** Displays container state, port bindings, and health check status.

---

### 5.3. Stop SQL Server Container
```powershell
docker compose down
```
- **Why It Is Required:** Frees up system RAM and CPU when testing is finished.
- **Purpose:** Gracefully terminates the SQL Server container.

---

## 6. Benchmarking & Verification Tools

### 6.1. Multi-Threaded Benchmark Suite
```powershell
powershell -ExecutionPolicy Bypass -File .\tools\benchmark.ps1
```
- **Why It Is Required:** Measures validation throughput and demonstrates the performance benefits of parallel processing.
- **Purpose:** Executes sequential vs parallel runs across 1, 2, 4, and 8 worker threads and displays throughput metrics (rules/sec, execution ms).

---

### 6.2. Ground-Truth Seed Verification Pipeline
```powershell
powershell -ExecutionPolicy Bypass -File .\tools\seed_verification.ps1
```
- **Why It Is Required:** End-to-end integration test validating the entire stack against known seeded data anomalies.
- **Purpose:** Runs CLI validation against the sample database and cross-references results against `db/seed/injected-issues.json`.

---

## 7. Process Management & Troubleshooting

### 7.1. Stop Backend API Process (Release File Locks)
```powershell
Stop-Process -Name "DataGuard.Api" -Force -ErrorAction SilentlyContinue
```
- **Why It Is Required:** If the API is running in the background, rebuilds of the solution will fail with `MSB3021: The process cannot access the file ... DataGuard.Core.dll because it is being used by another process`.
- **Purpose:** Terminates any running instance of `DataGuard.Api` immediately so files can be recompiled.

---

### 7.2. Check Backend Health Endpoint
```powershell
Invoke-RestMethod -Uri "http://localhost:5000/api/v1/health"
```
- **Why It Is Required:** Verifies that the API server is up and responsive before launching the frontend or initiating CLI calls.
- **Purpose:** Sends a `GET` request to `/api/v1/health` and returns `{ "status": "Healthy", "timestamp": "..." }`.

---

## 8. Interactive URLs & Endpoints

| Resource | URL | Purpose |
| :--- | :--- | :--- |
| **Web Dashboard** | `http://localhost:5173` | Interactive visual dashboard for runs, defect triage, and live execution |
| **Swagger API Docs** | `http://localhost:5000/swagger` | Interactive OpenAPI documentation for exploring and testing endpoints |
| **API Health Check** | `http://localhost:5000/api/v1/health` | Service liveness probe |
| **List Runs Endpoint** | `http://localhost:5000/api/v1/runs?top=10` | JSON list of historical validation runs |
| **List Defects Endpoint** | `http://localhost:5000/api/v1/defects` | JSON list of all tracked and deduplicated defects |
| **List Rules Endpoint** | `http://localhost:5000/api/v1/rules` | JSON list of active validation rules |
