-- ============================================================================
-- DataGuard Analytical Views
-- Script: 004_views.sql
-- Description: Advanced analytical views with window functions (LAG, ROW_NUMBER, DENSE_RANK).
-- ============================================================================

USE DataGuardDB;
GO

-- 1. vw_RunResultClassified
-- Classifies every rule result run-over-run as New, Recurring, Fixed, or SteadyPass
CREATE OR ALTER VIEW dbo.vw_RunResultClassified
AS
WITH PartitionedResults AS (
    SELECT 
        rr.ResultId,
        rr.RunId,
        rr.RuleId,
        rr.Status,
        rr.ExecutionTimeMs,
        rr.DiscrepancyCount,
        rr.Fingerprint,
        rr.ExecutedAtUtc,
        r.TriggeredBy,
        rul.RuleName,
        rul.RuleType,
        rul.Severity,
        rul.TargetTable,
        rul.TargetColumn,
        LAG(rr.Status) OVER (
            PARTITION BY rr.Fingerprint 
            ORDER BY rr.ExecutedAtUtc ASC, rr.ResultId ASC
        ) AS PreviousStatus,
        LAG(rr.RunId) OVER (
            PARTITION BY rr.Fingerprint 
            ORDER BY rr.ExecutedAtUtc ASC, rr.ResultId ASC
        ) AS PreviousRunId,
        ROW_NUMBER() OVER (
            PARTITION BY rr.Fingerprint 
            ORDER BY rr.ExecutedAtUtc DESC, rr.ResultId DESC
        ) AS FingerprintLatestSeq
    FROM dbo.RunResults rr
    INNER JOIN dbo.Runs r ON rr.RunId = r.RunId
    INNER JOIN dbo.Rules rul ON rr.RuleId = rul.RuleId
)
SELECT 
    ResultId,
    RunId,
    RuleId,
    RuleName,
    RuleType,
    Severity,
    TargetTable,
    TargetColumn,
    Status AS CurrentStatus,
    PreviousStatus,
    ExecutionTimeMs,
    DiscrepancyCount,
    Fingerprint,
    ExecutedAtUtc,
    CASE 
        WHEN Status IN ('Failed', 'Error') AND (PreviousStatus IS NULL OR PreviousStatus = 'Passed') THEN 'New'
        WHEN Status IN ('Failed', 'Error') AND PreviousStatus IN ('Failed', 'Error') THEN 'Recurring'
        WHEN Status = 'Passed' AND PreviousStatus IN ('Failed', 'Error') THEN 'Fixed'
        WHEN Status = 'Passed' AND (PreviousStatus = 'Passed' OR PreviousStatus IS NULL) THEN 'SteadyPass'
        ELSE 'Unknown'
    END AS Classification,
    FingerprintLatestSeq
FROM PartitionedResults;
GO

-- 2. vw_PassRateTrend
CREATE OR ALTER VIEW dbo.vw_PassRateTrend
AS
SELECT 
    RunId,
    StartedAtUtc,
    CAST(StartedAtUtc AS DATE) AS RunDate,
    TotalRules,
    PassedRules,
    FailedRules,
    ErrorRules,
    CASE WHEN TotalRules > 0 
         THEN CAST(ROUND((CAST(PassedRules AS FLOAT) / TotalRules) * 100.0, 2) AS DECIMAL(5,2))
         ELSE 0.0 END AS PassRatePercent,
    DurationMs
FROM dbo.Runs
WHERE Status IN ('Completed', 'Failed');
GO

-- 3. vw_TopFailingTables
-- Ranks tables with the highest composite defect risk score
CREATE OR ALTER VIEW dbo.vw_TopFailingTables
AS
WITH TableDefectSummary AS (
    SELECT 
        TargetTable,
        COUNT(*) AS TotalActiveDefects,
        SUM(CASE WHEN Severity = 'Critical' THEN 1 ELSE 0 END) AS CriticalCount,
        SUM(CASE WHEN Severity = 'High' THEN 1 ELSE 0 END) AS HighCount,
        SUM(CASE WHEN Severity = 'Medium' THEN 1 ELSE 0 END) AS MediumCount,
        SUM(CASE WHEN Severity = 'Low' THEN 1 ELSE 0 END) AS LowCount,
        SUM(dbo.fn_SeverityWeight(Severity) * OccurrenceCount) AS WeightedRiskScore,
        MAX(LastSeenAtUtc) AS LastDefectSeenAtUtc
    FROM dbo.Defects
    WHERE Status IN ('Open', 'Acknowledged', 'Reopened')
    GROUP BY TargetTable
)
SELECT 
    TargetTable,
    TotalActiveDefects,
    CriticalCount,
    HighCount,
    MediumCount,
    LowCount,
    WeightedRiskScore,
    LastDefectSeenAtUtc,
    DENSE_RANK() OVER (ORDER BY WeightedRiskScore DESC, TotalActiveDefects DESC) AS RiskRank
FROM TableDefectSummary;
GO

-- 4. vw_MeanTimeToResolve
CREATE OR ALTER VIEW dbo.vw_MeanTimeToResolve
AS
SELECT 
    Severity,
    COUNT(*) AS ResolvedDefectCount,
    AVG(DATEDIFF(MINUTE, FirstSeenAtUtc, ResolvedAtUtc)) AS AvgResolutionMinutes,
    AVG(DATEDIFF(HOUR, FirstSeenAtUtc, ResolvedAtUtc)) AS AvgResolutionHours,
    MIN(DATEDIFF(MINUTE, FirstSeenAtUtc, ResolvedAtUtc)) AS MinResolutionMinutes,
    MAX(DATEDIFF(MINUTE, FirstSeenAtUtc, ResolvedAtUtc)) AS MaxResolutionMinutes
FROM dbo.Defects
WHERE Status = 'Resolved' AND ResolvedAtUtc IS NOT NULL
GROUP BY Severity;
GO
