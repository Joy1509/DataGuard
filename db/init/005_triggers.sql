-- ============================================================================
-- DataGuard Triggers
-- Script: 005_triggers.sql
-- Description: Audit logging and state integrity triggers for Defects table.
-- ============================================================================

USE DataGuardDB;
GO

-- 1. trg_DefectAudit
-- Logs every change in Defect Status or Severity to DefectHistory
CREATE OR ALTER TRIGGER dbo.trg_DefectAudit
ON dbo.Defects
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    -- Handle Inserts (new defects)
    IF NOT EXISTS (SELECT 1 FROM deleted)
    BEGIN
        INSERT INTO dbo.DefectHistory (
            DefectId,
            PreviousStatus,
            NewStatus,
            PreviousSeverity,
            NewSeverity,
            ChangedBy,
            ChangeReason,
            ChangedAtUtc
        )
        SELECT 
            i.DefectId,
            NULL,
            i.Status,
            NULL,
            i.Severity,
            'System',
            'Initial defect creation',
            SYSUTCDATETIME()
        FROM inserted i;

        RETURN;
    END

    -- Handle Updates where Status or Severity changed
    INSERT INTO dbo.DefectHistory (
        DefectId,
        PreviousStatus,
        NewStatus,
        PreviousSeverity,
        NewSeverity,
        ChangedBy,
        ChangeReason,
        ChangedAtUtc
    )
    SELECT 
        i.DefectId,
        d.Status AS PreviousStatus,
        i.Status AS NewStatus,
        d.Severity AS PreviousSeverity,
        i.Severity AS NewSeverity,
        'System',
        CASE 
            WHEN d.Status <> i.Status AND d.Severity <> i.Severity 
                THEN 'Status changed from ' + d.Status + ' to ' + i.Status + '; Severity changed from ' + d.Severity + ' to ' + i.Severity
            WHEN d.Status <> i.Status 
                THEN 'Status transition: ' + d.Status + ' -> ' + i.Status
            WHEN d.Severity <> i.Severity 
                THEN 'Severity updated: ' + d.Severity + ' -> ' + i.Severity
            ELSE 'Defect recurring occurrence update'
        END,
        SYSUTCDATETIME()
    FROM inserted i
    INNER JOIN deleted d ON i.DefectId = d.DefectId
    WHERE i.Status <> d.Status OR i.Severity <> d.Severity;
END;
GO

-- 2. trg_PreventClosedDefectModification
-- Prevents editing resolved or wontfix defects directly unless explicitly reopening them
CREATE OR ALTER TRIGGER dbo.trg_PreventClosedDefectModification
ON dbo.Defects
FOR UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1 
        FROM deleted d
        INNER JOIN inserted i ON d.DefectId = i.DefectId
        WHERE d.Status IN ('Resolved', 'WontFix')
          AND i.Status IN ('Resolved', 'WontFix')
          AND (
              i.TargetTable <> d.TargetTable OR 
              ISNULL(i.TargetColumn, '') <> ISNULL(d.TargetColumn, '') OR 
              i.RuleId <> d.RuleId OR
              i.FirstSeenRunId <> d.FirstSeenRunId
          )
    )
    BEGIN
        RAISERROR('Direct modification of immutable properties on Resolved or WontFix defects is prohibited.', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END
END;
GO
