-- ============================================================================
-- DataGuard Stored Procedures
-- Script: 003_stored_procedures.sql
-- Description: Transactional procedures for run lifecycles, defect state tracking, and summaries.
-- ============================================================================

USE DataGuardDB;
GO

-- 1. usp_StartRun
CREATE OR ALTER PROCEDURE dbo.usp_StartRun
    @SourceConnectionId INT = NULL,
    @TargetConnectionId INT,
    @TriggeredBy NVARCHAR(100) = 'System',
    @TotalRules INT = 0,
    @RunId UNIQUEIDENTIFIER OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        SET @RunId = NEWID();

        INSERT INTO dbo.Runs (
            RunId,
            SourceConnectionId,
            TargetConnectionId,
            TriggeredBy,
            Status,
            TotalRules,
            PassedRules,
            FailedRules,
            ErrorRules,
            StartedAtUtc
        )
        VALUES (
            @RunId,
            @SourceConnectionId,
            @TargetConnectionId,
            @TriggeredBy,
            'Running',
            @TotalRules,
            0,
            0,
            0,
            SYSUTCDATETIME()
        );

        SELECT @RunId AS RunId;
    END TRY
    BEGIN CATCH
        THROW;
    END CATCH
END;
GO

-- 2. usp_RecordResult
CREATE OR ALTER PROCEDURE dbo.usp_RecordResult
    @RunId UNIQUEIDENTIFIER,
    @RuleId NVARCHAR(100),
    @Status NVARCHAR(20),
    @ExecutionTimeMs BIGINT,
    @SourceValue NVARCHAR(MAX) = NULL,
    @TargetValue NVARCHAR(MAX) = NULL,
    @DiscrepancyCount BIGINT = 0,
    @ErrorMessage NVARCHAR(MAX) = NULL,
    @Fingerprint NVARCHAR(64),
    @SampleRowsJson NVARCHAR(MAX) = NULL, -- JSON array of sample rows e.g. [{"row": 1, ...}]
    @ResultId UNIQUEIDENTIFIER OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        SET @ResultId = NEWID();

        INSERT INTO dbo.RunResults (
            ResultId,
            RunId,
            RuleId,
            Status,
            ExecutionTimeMs,
            SourceValue,
            TargetValue,
            DiscrepancyCount,
            ErrorMessage,
            ExecutedAtUtc,
            Fingerprint
        )
        VALUES (
            @ResultId,
            @RunId,
            @RuleId,
            @Status,
            @ExecutionTimeMs,
            @SourceValue,
            @TargetValue,
            @DiscrepancyCount,
            @ErrorMessage,
            SYSUTCDATETIME(),
            @Fingerprint
        );

        -- Ingest sample bad rows if provided (capped at 20)
        IF @SampleRowsJson IS NOT NULL AND ISJSON(@SampleRowsJson) = 1
        BEGIN
            INSERT INTO dbo.SampleRows (ResultId, RowIndex, RowJson, CapturedAtUtc)
            SELECT TOP 20
                @ResultId,
                TRY_CONVERT(INT, [key]) + 1,
                [value],
                SYSUTCDATETIME()
            FROM OPENJSON(@SampleRowsJson);
        END;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

-- 3. usp_UpsertDefect
CREATE OR ALTER PROCEDURE dbo.usp_UpsertDefect
    @Fingerprint NVARCHAR(64),
    @RuleId NVARCHAR(100),
    @TargetTable NVARCHAR(255),
    @TargetColumn NVARCHAR(255) = NULL,
    @Severity NVARCHAR(20),
    @RunId UNIQUEIDENTIFIER,
    @DefectId UNIQUEIDENTIFIER OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @ExistingDefectId UNIQUEIDENTIFIER;
        DECLARE @CurrentStatus NVARCHAR(30);

        SELECT 
            @ExistingDefectId = DefectId,
            @CurrentStatus = Status
        FROM dbo.Defects WITH (UPDLOCK, ROWLOCK)
        WHERE Fingerprint = @Fingerprint;

        IF @ExistingDefectId IS NULL
        BEGIN
            -- New defect
            SET @DefectId = NEWID();

            INSERT INTO dbo.Defects (
                DefectId,
                Fingerprint,
                RuleId,
                TargetTable,
                TargetColumn,
                Severity,
                Status,
                FirstSeenRunId,
                LastSeenRunId,
                OccurrenceCount,
                RegressionCount,
                FirstSeenAtUtc,
                LastSeenAtUtc
            )
            VALUES (
                @DefectId,
                @Fingerprint,
                @RuleId,
                @TargetTable,
                @TargetColumn,
                @Severity,
                'Open',
                @RunId,
                @RunId,
                1,
                0,
                SYSUTCDATETIME(),
                SYSUTCDATETIME()
            );
        END
        ELSE
        BEGIN
            -- Recurring or Reopened defect
            SET @DefectId = @ExistingDefectId;

            IF @CurrentStatus = 'Resolved'
            BEGIN
                -- Regression: previously fixed, now failing again
                UPDATE dbo.Defects
                SET 
                    Status = 'Reopened',
                    Severity = @Severity,
                    LastSeenRunId = @RunId,
                    ResolvedRunId = NULL,
                    ResolvedAtUtc = NULL,
                    OccurrenceCount = OccurrenceCount + 1,
                    RegressionCount = RegressionCount + 1,
                    LastSeenAtUtc = SYSUTCDATETIME()
                WHERE DefectId = @ExistingDefectId;
            END
            ELSE
            BEGIN
                -- Recurring failure
                UPDATE dbo.Defects
                SET 
                    Severity = @Severity,
                    LastSeenRunId = @RunId,
                    OccurrenceCount = OccurrenceCount + 1,
                    LastSeenAtUtc = SYSUTCDATETIME()
                WHERE DefectId = @ExistingDefectId;
            END
        END

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

-- 4. usp_CloseResolvedDefects
-- Automatically closes open/reopened defects for rules that passed in the specified run
CREATE OR ALTER PROCEDURE dbo.usp_CloseResolvedDefects
    @RunId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE d
        SET 
            d.Status = 'Resolved',
            d.ResolvedRunId = @RunId,
            d.ResolvedAtUtc = SYSUTCDATETIME()
        FROM dbo.Defects d
        INNER JOIN dbo.RunResults r ON d.Fingerprint = r.Fingerprint
        WHERE r.RunId = @RunId
          AND r.Status = 'Passed'
          AND d.Status IN ('Open', 'Acknowledged', 'Reopened');

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

-- 5. usp_CompleteRun
CREATE OR ALTER PROCEDURE dbo.usp_CompleteRun
    @RunId UNIQUEIDENTIFIER,
    @ExitCode INT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Passed INT = 0, @Failed INT = 0, @Error INT = 0, @Total INT = 0;
        DECLARE @StartedAt DATETIME2(7);

        SELECT 
            @Passed = COUNT(CASE WHEN Status = 'Passed' THEN 1 END),
            @Failed = COUNT(CASE WHEN Status = 'Failed' THEN 1 END),
            @Error  = COUNT(CASE WHEN Status = 'Error'  THEN 1 END),
            @Total  = COUNT(*)
        FROM dbo.RunResults
        WHERE RunId = @RunId;

        SELECT @StartedAt = StartedAtUtc
        FROM dbo.Runs
        WHERE RunId = @RunId;

        DECLARE @Now DATETIME2(7) = SYSUTCDATETIME();
        DECLARE @DurationMs BIGINT = DATEDIFF_BIG(MILLISECOND, @StartedAt, @Now);

        UPDATE dbo.Runs
        SET 
            Status = CASE WHEN @Error > 0 THEN 'Failed' ELSE 'Completed' END,
            TotalRules = @Total,
            PassedRules = @Passed,
            FailedRules = @Failed,
            ErrorRules = @Error,
            CompletedAtUtc = @Now,
            DurationMs = @DurationMs,
            ExitCode = @ExitCode
        WHERE RunId = @RunId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

-- 6. usp_GetRunSummary
CREATE OR ALTER PROCEDURE dbo.usp_GetRunSummary
    @RunId UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        IF @RunId IS NULL
        BEGIN
            SELECT TOP 1 @RunId = RunId 
            FROM dbo.Runs 
            ORDER BY StartedAtUtc DESC;
        END

        SELECT 
            r.RunId,
            r.TriggeredBy,
            r.Status,
            r.TotalRules,
            r.PassedRules,
            r.FailedRules,
            r.ErrorRules,
            CASE WHEN r.TotalRules > 0 
                 THEN CAST(ROUND((CAST(r.PassedRules AS FLOAT) / r.TotalRules) * 100.0, 2) AS DECIMAL(5,2))
                 ELSE 0.0 END AS PassRatePercent,
            r.StartedAtUtc,
            r.CompletedAtUtc,
            r.DurationMs,
            r.ExitCode,
            src.ConnectionName AS SourceConnectionName,
            tgt.ConnectionName AS TargetConnectionName
        FROM dbo.Runs r
        LEFT JOIN dbo.Connections src ON r.SourceConnectionId = src.ConnectionId
        INNER JOIN dbo.Connections tgt ON r.TargetConnectionId = tgt.ConnectionId
        WHERE r.RunId = @RunId;

        -- Critical and High Failures for this run
        SELECT 
            rr.ResultId,
            rr.RuleId,
            rul.RuleName,
            rul.RuleType,
            rul.Severity,
            rul.TargetTable,
            rul.TargetColumn,
            rr.Status,
            rr.DiscrepancyCount,
            rr.ExecutionTimeMs,
            rr.ErrorMessage,
            rr.Fingerprint
        FROM dbo.RunResults rr
        INNER JOIN dbo.Rules rul ON rr.RuleId = rul.RuleId
        WHERE rr.RunId = @RunId
        ORDER BY dbo.fn_SeverityWeight(rul.Severity) DESC, rr.DiscrepancyCount DESC;
    END TRY
    BEGIN CATCH
        THROW;
    END CATCH
END;
GO

-- 7. usp_GetTrend
CREATE OR ALTER PROCEDURE dbo.usp_GetTrend
    @TopRuns INT = 30
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        SELECT TOP (@TopRuns)
            RunId,
            StartedAtUtc,
            DurationMs,
            TotalRules,
            PassedRules,
            FailedRules,
            ErrorRules,
            CASE WHEN TotalRules > 0 
                 THEN CAST(ROUND((CAST(PassedRules AS FLOAT) / TotalRules) * 100.0, 2) AS DECIMAL(5,2))
                 ELSE 0.0 END AS PassRatePercent
        FROM dbo.Runs
        WHERE Status IN ('Completed', 'Failed')
        ORDER BY StartedAtUtc DESC;
    END TRY
    BEGIN CATCH
        THROW;
    END CATCH
END;
GO
