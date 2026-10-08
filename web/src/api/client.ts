import { Defect, RuleDefinition, RunSummary } from '../types';

const API_BASE = '/api/v1';

export async function fetchHealth(): Promise<{ status: string; timestamp: string; version: string }> {
  const res = await fetch(`${API_BASE}/health`);
  if (!res.ok) throw new Error('Failed to fetch health');
  return res.json();
}

export async function fetchRuns(top = 30): Promise<RunSummary[]> {
  const res = await fetch(`${API_BASE}/runs?top=${top}`);
  if (!res.ok) throw new Error('Failed to fetch runs');
  return res.json();
}

export async function fetchRunDetail(runId: string): Promise<RunSummary> {
  const res = await fetch(`${API_BASE}/runs/${runId}`);
  if (!res.ok) throw new Error(`Failed to fetch run ${runId}`);
  return res.json();
}

export async function triggerRun(payload: { rulesPath?: string; parallelism?: number }): Promise<{ runId: string; status: string }> {
  const res = await fetch(`${API_BASE}/runs`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload)
  });
  if (!res.ok) throw new Error('Failed to trigger run');
  return res.json();
}

export async function fetchRules(): Promise<RuleDefinition[]> {
  const res = await fetch(`${API_BASE}/rules`);
  if (!res.ok) throw new Error('Failed to fetch rules');
  return res.json();
}

export async function fetchDefects(status?: string, severity?: string): Promise<Defect[]> {
  const params = new URLSearchParams();
  if (status) params.append('status', status);
  if (severity) params.append('severity', severity);
  const res = await fetch(`${API_BASE}/defects?${params.toString()}`);
  if (!res.ok) throw new Error('Failed to fetch defects');
  return res.json();
}

export async function fetchTrend(): Promise<Array<{ runId: string; startedAtUtc: string; passRatePercent: number; totalRules: number; failedRules: number; durationMs: number }>> {
  const res = await fetch(`${API_BASE}/metrics/trend`);
  if (!res.ok) throw new Error('Failed to fetch trend');
  return res.json();
}
