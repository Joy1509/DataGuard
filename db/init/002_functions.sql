-- ============================================================================
-- DataGuard User Defined Functions
-- Script: 002_functions.sql
-- Description: Deterministic functions for fingerprinting and severity weighting.
-- ============================================================================

USE DataGuardDB;
GO

-- 1. fn_DefectFingerprint
-- Generates a deterministic SHA2-256 hex string for (RuleId + TargetTable + TargetColumn)
CREATE OR ALTER FUNCTION dbo.fn_DefectFingerprint
(
    @RuleId NVARCHAR(100),
    @TargetTable NVARCHAR(255),
    @TargetColumn NVARCHAR(255)
)
RETURNS NVARCHAR(64)
WITH SCHEMABINDING
AS
BEGIN
    DECLARE @RawInput NVARCHAR(1000) = 
        LOWER(LTRIM(RTRIM(@RuleId))) + '|' + 
        LOWER(LTRIM(RTRIM(@TargetTable))) + '|' + 
        ISNULL(LOWER(LTRIM(RTRIM(@TargetColumn))), '');

    RETURN CONVERT(NVARCHAR(64), HASHBYTES('SHA2_256', @RawInput), 2);
END;
GO

-- 2. fn_SeverityWeight
-- Numeric weight for computing composite data quality indexes and risk scoring
CREATE OR ALTER FUNCTION dbo.fn_SeverityWeight
(
    @Severity NVARCHAR(20)
)
RETURNS INT
WITH SCHEMABINDING
AS
BEGIN
    RETURN CASE UPPER(@Severity)
        WHEN 'CRITICAL' THEN 4
        WHEN 'HIGH'     THEN 3
        WHEN 'MEDIUM'   THEN 2
        WHEN 'LOW'      THEN 1
        ELSE 0
    END;
END;
GO
