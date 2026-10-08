-- ============================================================================
-- DataGuard Metadata Database Schema
-- Script: 001_schema.sql
-- Description: Idempotent initialization script for core DataGuard schema.
-- ============================================================================

IF DB_ID('DataGuardDB') IS NULL
BEGIN
    CREATE DATABASE DataGuardDB;
END
GO

USE DataGuardDB;
GO

-- 1. Connections Table
IF OBJECT_ID('dbo.Connections', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Connections (
        ConnectionId INT IDENTITY(1,1) CONSTRAINT PK_Connections PRIMARY KEY CLUSTERED,
        ConnectionName NVARCHAR(100) NOT NULL CONSTRAINT UQ_Connections_Name UNIQUE,
        ConnectionStringKey NVARCHAR(200) NOT NULL,
        DatabaseType NVARCHAR(50) NOT NULL CONSTRAINT DF_Connections_Type DEFAULT 'SqlServer',
        CreatedAtUtc DATETIME2(7) NOT NULL CONSTRAINT DF_Connections_CreatedAt DEFAULT SYSUTCDATETIME()
    );
END
GO

-- 2. Rules Table
IF OBJECT_ID('dbo.Rules', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Rules (
        RuleId NVARCHAR(100) NOT NULL CONSTRAINT PK_Rules PRIMARY KEY CLUSTERED,
        RuleName NVARCHAR(200) NOT NULL,
        Description NVARCHAR(1000) NULL,
        RuleType NVARCHAR(50) NOT NULL,
        Severity NVARCHAR(20) NOT NULL,
        IsEnabled BIT NOT NULL CONSTRAINT DF_Rules_IsEnabled DEFAULT 1,
        SourceTable NVARCHAR(255) NULL,
        TargetTable NVARCHAR(255) NOT NULL,
        TargetColumn NVARCHAR(255) NULL,
        ConfigurationJson NVARCHAR(MAX) NOT NULL,
        CreatedAtUtc DATETIME2(7) NOT NULL CONSTRAINT DF_Rules_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(7) NOT NULL CONSTRAINT DF_Rules_UpdatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_Rules_RuleType CHECK (RuleType IN ('RowCount', 'ColumnAggregate', 'Checksum', 'NotNull', 'DuplicateKey', 'OrphanKey', 'ValueCheck', 'SchemaDrift', 'CustomSql')),
        CONSTRAINT CK_Rules_Severity CHECK (Severity IN ('Critical', 'High', 'Medium', 'Low'))
    );
END
GO

-- 3. Runs Table
IF OBJECT_ID('dbo.Runs', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Runs (
        RunId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Runs PRIMARY KEY CLUSTERED DEFAULT NEWID(),
        SourceConnectionId INT NULL CONSTRAINT FK_Runs_SourceConnection FOREIGN KEY REFERENCES dbo.Connections(ConnectionId),
        TargetConnectionId INT NOT NULL CONSTRAINT FK_Runs_TargetConnection FOREIGN KEY REFERENCES dbo.Connections(ConnectionId),
        TriggeredBy NVARCHAR(100) NOT NULL CONSTRAINT DF_Runs_TriggeredBy DEFAULT 'System',
        Status NVARCHAR(30) NOT NULL CONSTRAINT DF_Runs_Status DEFAULT 'Running',
        TotalRules INT NOT NULL CONSTRAINT DF_Runs_TotalRules DEFAULT 0,
        PassedRules INT NOT NULL CONSTRAINT DF_Runs_PassedRules DEFAULT 0,
        FailedRules INT NOT NULL CONSTRAINT DF_Runs_FailedRules DEFAULT 0,
        ErrorRules INT NOT NULL CONSTRAINT DF_Runs_ErrorRules DEFAULT 0,
        StartedAtUtc DATETIME2(7) NOT NULL CONSTRAINT DF_Runs_StartedAt DEFAULT SYSUTCDATETIME(),
        CompletedAtUtc DATETIME2(7) NULL,
        DurationMs BIGINT NULL,
        ExitCode INT NULL,
        CONSTRAINT CK_Runs_Status CHECK (Status IN ('Running', 'Completed', 'Failed', 'Cancelled'))
    );
END
GO

-- 4. RunResults Table
IF OBJECT_ID('dbo.RunResults', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.RunResults (
        ResultId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_RunResults PRIMARY KEY CLUSTERED DEFAULT NEWID(),
        RunId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_RunResults_Runs FOREIGN KEY REFERENCES dbo.Runs(RunId) ON DELETE CASCADE,
        RuleId NVARCHAR(100) NOT NULL CONSTRAINT FK_RunResults_Rules FOREIGN KEY REFERENCES dbo.Rules(RuleId),
        Status NVARCHAR(20) NOT NULL,
        ExecutionTimeMs BIGINT NOT NULL,
        SourceValue NVARCHAR(MAX) NULL,
        TargetValue NVARCHAR(MAX) NULL,
        DiscrepancyCount BIGINT NOT NULL CONSTRAINT DF_RunResults_Discrepancy DEFAULT 0,
        ErrorMessage NVARCHAR(MAX) NULL,
        ExecutedAtUtc DATETIME2(7) NOT NULL CONSTRAINT DF_RunResults_ExecutedAt DEFAULT SYSUTCDATETIME(),
        Fingerprint NVARCHAR(64) NOT NULL,
        CONSTRAINT CK_RunResults_Status CHECK (Status IN ('Passed', 'Failed', 'Error', 'Skipped'))
    );
END
GO

-- 5. SampleRows Table (Capped at 20 bad rows per rule result)
IF OBJECT_ID('dbo.SampleRows', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SampleRows (
        SampleId BIGINT IDENTITY(1,1) CONSTRAINT PK_SampleRows PRIMARY KEY CLUSTERED,
        ResultId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_SampleRows_RunResults FOREIGN KEY REFERENCES dbo.RunResults(ResultId) ON DELETE CASCADE,
        RowIndex INT NOT NULL,
        RowJson NVARCHAR(MAX) NOT NULL,
        CapturedAtUtc DATETIME2(7) NOT NULL CONSTRAINT DF_SampleRows_CapturedAt DEFAULT SYSUTCDATETIME()
    );
END
GO

-- 6. Defects Table
IF OBJECT_ID('dbo.Defects', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Defects (
        DefectId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Defects PRIMARY KEY CLUSTERED DEFAULT NEWID(),
        Fingerprint NVARCHAR(64) NOT NULL CONSTRAINT UQ_Defects_Fingerprint UNIQUE,
        RuleId NVARCHAR(100) NOT NULL CONSTRAINT FK_Defects_Rules FOREIGN KEY REFERENCES dbo.Rules(RuleId),
        TargetTable NVARCHAR(255) NOT NULL,
        TargetColumn NVARCHAR(255) NULL,
        Severity NVARCHAR(20) NOT NULL,
        Status NVARCHAR(30) NOT NULL CONSTRAINT DF_Defects_Status DEFAULT 'Open',
        FirstSeenRunId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Defects_FirstRun FOREIGN KEY REFERENCES dbo.Runs(RunId),
        LastSeenRunId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Defects_LastRun FOREIGN KEY REFERENCES dbo.Runs(RunId),
        OccurrenceCount INT NOT NULL CONSTRAINT DF_Defects_Occurrence DEFAULT 1,
        RegressionCount INT NOT NULL CONSTRAINT DF_Defects_Regression DEFAULT 0,
        ResolvedRunId UNIQUEIDENTIFIER NULL CONSTRAINT FK_Defects_ResolvedRun FOREIGN KEY REFERENCES dbo.Runs(RunId),
        FirstSeenAtUtc DATETIME2(7) NOT NULL CONSTRAINT DF_Defects_FirstSeen DEFAULT SYSUTCDATETIME(),
        LastSeenAtUtc DATETIME2(7) NOT NULL CONSTRAINT DF_Defects_LastSeen DEFAULT SYSUTCDATETIME(),
        ResolvedAtUtc DATETIME2(7) NULL,
        CONSTRAINT CK_Defects_Status CHECK (Status IN ('Open', 'Acknowledged', 'Resolved', 'Reopened', 'WontFix')),
        CONSTRAINT CK_Defects_Severity CHECK (Severity IN ('Critical', 'High', 'Medium', 'Low'))
    );
END
GO

-- 7. DefectHistory Table (Audit trail for status and severity mutations)
IF OBJECT_ID('dbo.DefectHistory', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DefectHistory (
        HistoryId BIGINT IDENTITY(1,1) CONSTRAINT PK_DefectHistory PRIMARY KEY CLUSTERED,
        DefectId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_DefectHistory_Defects FOREIGN KEY REFERENCES dbo.Defects(DefectId) ON DELETE CASCADE,
        PreviousStatus NVARCHAR(30) NULL,
        NewStatus NVARCHAR(30) NOT NULL,
        PreviousSeverity NVARCHAR(20) NULL,
        NewSeverity NVARCHAR(20) NOT NULL,
        ChangedBy NVARCHAR(100) NOT NULL CONSTRAINT DF_DefectHistory_ChangedBy DEFAULT 'System',
        ChangeReason NVARCHAR(500) NULL,
        ChangedAtUtc DATETIME2(7) NOT NULL CONSTRAINT DF_DefectHistory_ChangedAt DEFAULT SYSUTCDATETIME()
    );
END
GO

-- Performance Indexes
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RunResults_RunId' AND object_id = OBJECT_ID('dbo.RunResults'))
    CREATE NONCLUSTERED INDEX IX_RunResults_RunId ON dbo.RunResults(RunId) INCLUDE (Status, ExecutionTimeMs, DiscrepancyCount, Fingerprint);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RunResults_Fingerprint_ExecutedAtUtc' AND object_id = OBJECT_ID('dbo.RunResults'))
    CREATE NONCLUSTERED INDEX IX_RunResults_Fingerprint_ExecutedAtUtc ON dbo.RunResults(Fingerprint, ExecutedAtUtc DESC) INCLUDE (RunId, Status);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Defects_Status_Severity' AND object_id = OBJECT_ID('dbo.Defects'))
    CREATE NONCLUSTERED INDEX IX_Defects_Status_Severity ON dbo.Defects(Status, Severity) INCLUDE (Fingerprint, RuleId, TargetTable, LastSeenAtUtc);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Defects_TargetTable' AND object_id = OBJECT_ID('dbo.Defects'))
    CREATE NONCLUSTERED INDEX IX_Defects_TargetTable ON dbo.Defects(TargetTable);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SampleRows_ResultId' AND object_id = OBJECT_ID('dbo.SampleRows'))
    CREATE NONCLUSTERED INDEX IX_SampleRows_ResultId ON dbo.SampleRows(ResultId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DefectHistory_DefectId' AND object_id = OBJECT_ID('dbo.DefectHistory'))
    CREATE NONCLUSTERED INDEX IX_DefectHistory_DefectId ON dbo.DefectHistory(DefectId, ChangedAtUtc DESC);
GO
