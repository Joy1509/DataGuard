export type RuleSeverity = 'Critical' | 'High' | 'Medium' | 'Low';
export type RuleType = 'RowCount' | 'ColumnAggregate' | 'Checksum' | 'NotNull' | 'DuplicateKey' | 'OrphanKey' | 'ValueCheck' | 'SchemaDrift' | 'CustomSql';
export type RuleStatus = 'Passed' | 'Failed' | 'Error' | 'Skipped';
export type RunStatus = 'Running' | 'Completed' | 'Failed' | 'Cancelled';
export type DefectStatus = 'Open' | 'Acknowledged' | 'Resolved' | 'Reopened' | 'WontFix';

export interface RuleResult {
  ruleId: string;
  ruleName: string;
  ruleType: RuleType;
  severity: RuleSeverity;
  targetTable: string;
  targetColumn?: string;
  fingerprint: string;
  status: RuleStatus;
  executionTimeMs: number;
  sourceValue?: string;
  targetValue?: string;
  discrepancyCount: number;
  errorMessage?: string;
  sampleRowsJson: string[];
}

export interface RunSummary {
  runId: string;
  triggeredBy: string;
  status: RunStatus;
  totalRules: number;
  passedRules: number;
  failedRules: number;
  errorRules: number;
  passRatePercent: number;
  startedAtUtc: string;
  completedAtUtc?: string;
  durationMs?: number;
  exitCode?: number;
  sourceConnectionName?: string;
  targetConnectionName?: string;
  results: RuleResult[];
}

export interface Defect {
  defectId: string;
  fingerprint: string;
  ruleId: string;
  targetTable: string;
  targetColumn?: string;
  severity: RuleSeverity;
  status: DefectStatus;
  firstSeenRunId: string;
  lastSeenRunId: string;
  occurrenceCount: number;
  regressionCount: number;
  resolvedRunId?: string;
  firstSeenAtUtc: string;
  lastSeenAtUtc: string;
  resolvedAtUtc?: string;
}

export interface RuleDefinition {
  id: string;
  name: string;
  description?: string;
  type: RuleType;
  severity: RuleSeverity;
  enabled: boolean;
  sourceTable?: string;
  targetTable: string;
  targetColumn?: string;
  timeoutSeconds: number;
}
