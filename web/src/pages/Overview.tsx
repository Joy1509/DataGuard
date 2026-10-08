import React from 'react';
import { RunSummary, Defect } from '../types';
import { CheckCircle2, AlertTriangle, Clock, ArrowRight, TrendingUp } from 'lucide-react';
import { ResponsiveContainer, AreaChart, Area, XAxis, YAxis, Tooltip, CartesianGrid } from 'recharts';

interface OverviewProps {
  runs: RunSummary[];
  defects: Defect[];
  onSelectRun: (runId: string) => void;
}

export const Overview: React.FC<OverviewProps> = ({ runs, defects, onSelectRun }) => {
  const latestRun = runs[0];
  const passRate = latestRun ? latestRun.passRatePercent : 100;
  const criticalDefects = defects.filter(d => d.severity === 'Critical' && d.status !== 'Resolved').length;
  const highDefects = defects.filter(d => d.severity === 'High' && d.status !== 'Resolved').length;

  const chartData = runs.slice(0, 15).reverse().map((r, i) => ({
    name: `Run #${i + 1}`,
    passRate: r.passRatePercent,
    duration: r.durationMs ?? 0,
    failed: r.failedRules
  }));

  return (
    <div className="space-y-6">
      {/* Top Metrics Cards */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <div className="bg-slate-900/60 border border-slate-800 rounded-xl p-5 backdrop-blur">
          <div className="flex items-center justify-between">
            <span className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Pass Rate</span>
            <CheckCircle2 className={`w-5 h-5 ${passRate >= 90 ? 'text-emerald-400' : 'text-amber-400'}`} />
          </div>
          <div className="mt-3 flex items-baseline space-x-2">
            <span className={`text-3xl font-bold tracking-tight ${passRate >= 90 ? 'text-emerald-400' : 'text-amber-400'}`}>{passRate}%</span>
            <span className="text-xs text-slate-400">latest run</span>
          </div>
          <div className="mt-3 w-full bg-slate-800 rounded-full h-1.5 overflow-hidden">
            <div className={`h-full ${passRate >= 90 ? 'bg-emerald-500' : 'bg-amber-500'}`} style={{ width: `${passRate}%` }}></div>
          </div>
        </div>

        <div className="bg-slate-900/60 border border-slate-800 rounded-xl p-5 backdrop-blur">
          <div className="flex items-center justify-between">
            <span className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Total Rules Executed</span>
            <TrendingUp className="w-5 h-5 text-sky-400" />
          </div>
          <div className="mt-3 flex items-baseline space-x-2">
            <span className="text-3xl font-bold tracking-tight text-white">{latestRun?.totalRules ?? 0}</span>
            <span className="text-xs text-emerald-400">{latestRun?.passedRules ?? 0} passing</span>
          </div>
          <p className="mt-3 text-xs text-slate-400">{latestRun?.failedRules ?? 0} failed / {latestRun?.errorRules ?? 0} errors</p>
        </div>

        <div className="bg-slate-900/60 border border-slate-800 rounded-xl p-5 backdrop-blur">
          <div className="flex items-center justify-between">
            <span className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Active Defects</span>
            <AlertTriangle className="w-5 h-5 text-rose-400" />
          </div>
          <div className="mt-3 flex items-baseline space-x-2">
            <span className="text-3xl font-bold tracking-tight text-rose-400">{defects.filter(d => d.status !== 'Resolved').length}</span>
            <span className="text-xs text-rose-300">({criticalDefects} Critical)</span>
          </div>
          <p className="mt-3 text-xs text-slate-400">{highDefects} High severity defects open</p>
        </div>

        <div className="bg-slate-900/60 border border-slate-800 rounded-xl p-5 backdrop-blur">
          <div className="flex items-center justify-between">
            <span className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Last Run Duration</span>
            <Clock className="w-5 h-5 text-indigo-400" />
          </div>
          <div className="mt-3 flex items-baseline space-x-2">
            <span className="text-3xl font-bold tracking-tight text-white">{latestRun?.durationMs ?? 0}</span>
            <span className="text-xs text-slate-400">ms</span>
          </div>
          <p className="mt-3 text-xs text-slate-400">Parallel execution across 4 threads</p>
        </div>
      </div>

      {/* Pass Rate Trend Chart */}
      <div className="bg-slate-900/60 border border-slate-800 rounded-xl p-6 backdrop-blur">
        <div className="flex items-center justify-between mb-4">
          <div>
            <h2 className="text-base font-bold text-white">Migration Quality & Pass Rate Trend</h2>
            <p className="text-xs text-slate-400">Run-over-run historical correctness</p>
          </div>
        </div>

        <div className="h-64 w-full">
          {chartData.length > 0 ? (
            <ResponsiveContainer width="100%" height="100%">
              <AreaChart data={chartData} margin={{ top: 10, right: 10, left: -20, bottom: 0 }}>
                <defs>
                  <linearGradient id="passRateGrad" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#0ea5e9" stopOpacity={0.4}/>
                    <stop offset="95%" stopColor="#0ea5e9" stopOpacity={0.0}/>
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" stroke="#1e293b" />
                <XAxis dataKey="name" stroke="#64748b" fontSize={11} />
                <YAxis stroke="#64748b" fontSize={11} domain={[0, 100]} />
                <Tooltip
                  contentStyle={{ backgroundColor: '#0f172a', borderColor: '#334155', borderRadius: '8px', color: '#f8fafc' }}
                  labelStyle={{ color: '#94a3b8' }}
                />
                <Area type="monotone" dataKey="passRate" stroke="#0ea5e9" strokeWidth={2} fillOpacity={1} fill="url(#passRateGrad)" name="Pass Rate %" />
              </AreaChart>
            </ResponsiveContainer>
          ) : (
            <div className="flex items-center justify-center h-full text-slate-500 text-sm">
              Trigger a validation run to view historical trend metrics.
            </div>
          )}
        </div>
      </div>

      {/* Recent Runs Table */}
      <div className="bg-slate-900/60 border border-slate-800 rounded-xl overflow-hidden backdrop-blur">
        <div className="px-6 py-4 border-b border-slate-800 flex items-center justify-between">
          <h2 className="text-base font-bold text-white">Recent Validation Runs</h2>
          <span className="text-xs text-slate-400">{runs.length} runs recorded</span>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead className="bg-slate-950/60 text-xs uppercase text-slate-400 font-semibold border-b border-slate-800">
              <tr>
                <th className="px-6 py-3">Run ID</th>
                <th className="px-6 py-3">Status</th>
                <th className="px-6 py-3 text-right">Pass Rate</th>
                <th className="px-6 py-3 text-right">Passed / Failed</th>
                <th className="px-6 py-3 text-right">Duration</th>
                <th className="px-6 py-3">Started</th>
                <th className="px-6 py-3 text-right">Action</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800">
              {runs.map(run => (
                <tr key={run.runId} className="hover:bg-slate-800/40 transition-colors">
                  <td className="px-6 py-4 font-mono text-xs text-slate-300 font-medium">{run.runId.substring(0, 8)}...</td>
                  <td className="px-6 py-4">
                    <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold ${
                      run.status === 'Completed' ? 'bg-emerald-500/10 text-emerald-400 border border-emerald-500/20' : 'bg-rose-500/10 text-rose-400 border border-rose-500/20'
                    }`}>
                      {run.status}
                    </span>
                  </td>
                  <td className="px-6 py-4 text-right font-bold font-mono">
                    <span className={run.passRatePercent >= 90 ? 'text-emerald-400' : 'text-amber-400'}>{run.passRatePercent}%</span>
                  </td>
                  <td className="px-6 py-4 text-right font-mono text-xs">
                    <span className="text-emerald-400">{run.passedRules}</span> / <span className="text-rose-400">{run.failedRules}</span>
                  </td>
                  <td className="px-6 py-4 text-right font-mono text-xs text-slate-400">{run.durationMs ?? 0} ms</td>
                  <td className="px-6 py-4 text-xs text-slate-400">{new Date(run.startedAtUtc).toLocaleTimeString()}</td>
                  <td className="px-6 py-4 text-right">
                    <button
                      onClick={() => onSelectRun(run.runId)}
                      className="inline-flex items-center space-x-1 text-xs text-sky-400 hover:text-sky-300 font-medium"
                    >
                      <span>View Results</span>
                      <ArrowRight className="w-3.5 h-3.5" />
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};
