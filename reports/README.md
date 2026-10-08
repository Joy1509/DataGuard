# DataGuard Reporting

DataGuard provides dual reporting capabilities:
1. **SSRS (.rdl)**: `DataQualitySummary.rdl` for enterprise Microsoft SQL Server Reporting Services environments. Connected directly to `dbo.usp_GetRunSummary` and `dbo.vw_RunResultClassified`.
2. **QuestPDF Engine**: High-fidelity, code-first PDF generator built into DataGuard.Api (`GET /api/v1/reports/pdf/{runId}`) and CLI exports.
