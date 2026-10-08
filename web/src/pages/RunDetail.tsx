import React, { useState } from 'react';
import { RunSummary, RuleResult } from '../types';
import { Download, Filter, Search, CheckCircle2, XCircle, ArrowLeft, Code } from 'lucide-react';

interface RunDetailProps {
  run: RunSummary;
  onBack: () => void;
}

export const RunDetail: React.FC<RunDetailProps> = ({ run, onBack }) => {
  const [statusFilter, setStatusFilter] = useState<string>('ALL');
  const [severityFilter, setSeverityFilter] = useState<string>('ALL');
  const [searchTerm, setSearchTerm] = useState('');
  const [selectedSampleResult, setSelectedSampleResult] = useState<RuleResult | null>(null);

  const filteredResults = (run.results || []).filter(r => {
    const matchesStatus = statusFilter === 'ALL' || r.status === statusFilter;
    const matchesSeverity = severityFilter === 'ALL' || r.severity === severityFilter;
    const matchesSearch = searchTerm === '' ||
      r.ruleName.toLowerCase().includes(searchTerm.toLowerCase()) ||
      r.ruleId.toLowerCase().includes(searchTerm.toLowerCase()) ||
      r.targetTable.toLowerCase().includes(searchTerm.toLowerCase());
    return matchesStatus && matchesSeverity && matchesSearch;
  });

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center space-x-4">
          <button
            onClick={onBack}
            className="p-2 rounded-lg bg-slate-900 border border-slate-800 text-slate-400 hover:text-white transition-colors"
          >
            <ArrowLeft className="w-5 h-5" />
          </button>
          <div>
            <div className="flex items-center space-x-3">
              <h1 className="text-xl font-bold text-white">Run Details</h1>
              <span className="font-mono text-xs text-sky-400 bg-sky-500/10 border border-sky-500/20 px-2 py-0.5 rounded">
                {run.runId}
              </span>
            </div>
            <p className="text-xs text-slate-400 mt-0.5">
              Executed at {new Date(run.startedAtUtc).toLocaleString()} UTC ({run.durationMs ?? 0} ms)
            </p>
          </div>
        </div>

        <a
          href={`/api/v1/reports/pdf/${run.runId}`}
          target="_blank"
          rel="noopener noreferrer"
          className="flex items-center space-x-2 bg-slate-900 hover:bg-slate-800 border border-slate-800 text-slate-200 text-sm font-medium px-4 py-2 rounded-lg transition-colors"
        >
          <Download className="w-4 h-4 text-sky-400" />
          <span>Export PDF Report</span>
        </a>
      </div>

      {/* Filters Bar */}
      <div className="bg-slate-900/60 border border-slate-800 rounded-xl p-4 flex flex-wrap items-center justify-between gap-4">
        <div className="flex items-center space-x-3 flex-1 min-w-[240px]">
          <Search className="w-4 h-4 text-slate-500" />
          <input
            type="text"
            placeholder="Search rule ID, name, or target table..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="bg-transparent border-none text-sm text-slate-200 placeholder-slate-500 focus:outline-none w-full"
          />
        </div>

        <div className="flex items-center space-x-3">
          <div className="flex items-center space-x-1.5 text-xs text-slate-400">
            <Filter className="w-3.5 h-3.5" />
            <span>Status:</span>
          </div>
          <select
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value)}
            className="bg-slate-950 border border-slate-800 rounded-lg px-2.5 py-1.5 text-xs text-slate-300 focus:outline-none"
          >
            <option value="ALL">All Statuses</option>
            <option value="Passed">Passed</option>
            <option value="Failed">Failed</option>
            <option value="Error">Error</option>
          </select>

          <select
            value={severityFilter}
            onChange={(e) => setSeverityFilter(e.target.value)}
            className="bg-slate-950 border border-slate-800 rounded-lg px-2.5 py-1.5 text-xs text-slate-300 focus:outline-none"
          >
            <option value="ALL">All Severities</option>
            <option value="Critical">Critical</option>
            <option value="High">High</option>
            <option value="Medium">Medium</option>
            <option value="Low">Low</option>
          </select>
        </div>
      </div>

      {/* Results Table */}
      <div className="bg-slate-900/60 border border-slate-800 rounded-xl overflow-hidden backdrop-blur">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead className="bg-slate-950/60 text-xs uppercase text-slate-400 font-semibold border-b border-slate-800">
              <tr>
                <th className="px-6 py-3">Status</th>
                <th className="px-6 py-3">Rule ID</th>
                <th className="px-6 py-3">Rule Name</th>
                <th className="px-6 py-3">Type</th>
                <th className="px-6 py-3">Severity</th>
                <th className="px-6 py-3">Target Table</th>
                <th className="px-6 py-3 text-right">Discrepancy</th>
                <th className="px-6 py-3 text-right">Duration</th>
                <th className="px-6 py-3 text-center">Samples</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800">
              {filteredResults.map(r => (
                <tr key={r.ruleId} className="hover:bg-slate-800/40 transition-colors">
                  <td className="px-6 py-4">
                    <span className={`inline-flex items-center space-x-1.5 px-2.5 py-0.5 rounded-full text-xs font-semibold ${
                      r.status === 'Passed'
                        ? 'bg-emerald-500/10 text-emerald-400 border border-emerald-500/20'
                        : 'bg-rose-500/10 text-rose-400 border border-rose-500/20'
                    }`}>
                      {r.status === 'Passed' ? <CheckCircle2 className="w-3.5 h-3.5" /> : <XCircle className="w-3.5 h-3.5" />}
                      <span>{r.status}</span>
                    </span>
                  </td>
                  <td className="px-6 py-4 font-mono text-xs text-slate-300 font-semibold">{r.ruleId}</td>
                  <td className="px-6 py-4 text-slate-200">{r.ruleName}</td>
                  <td className="px-6 py-4 text-xs font-mono text-slate-400">{r.ruleType}</td>
                  <td className="px-6 py-4">
                    <span className={`text-xs font-semibold px-2 py-0.5 rounded ${
                      r.severity === 'Critical' ? 'bg-rose-500/10 text-rose-400 border border-rose-500/20' :
                      r.severity === 'High' ? 'bg-amber-500/10 text-amber-400 border border-amber-500/20' :
                      r.severity === 'Medium' ? 'bg-yellow-500/10 text-yellow-400 border border-yellow-500/20' :
                      'bg-blue-500/10 text-blue-400 border border-blue-500/20'
                    }`}>
                      {r.severity}
                    </span>
                  </td>
                  <td className="px-6 py-4 font-mono text-xs text-slate-400">{r.targetTable}</td>
                  <td className="px-6 py-4 text-right font-mono text-xs">
                    {r.discrepancyCount > 0 ? (
                      <span className="text-rose-400 font-bold">{r.discrepancyCount.toLocaleString()}</span>
                    ) : (
                      <span className="text-emerald-400">0</span>
                    )}
                  </td>
                  <td className="px-6 py-4 text-right font-mono text-xs text-slate-400">{r.executionTimeMs} ms</td>
                  <td className="px-6 py-4 text-center">
                    {r.sampleRowsJson && r.sampleRowsJson.length > 0 ? (
                      <button
                        onClick={() => setSelectedSampleResult(r)}
                        className="inline-flex items-center space-x-1 text-xs text-sky-400 hover:text-sky-300 bg-sky-500/10 border border-sky-500/20 px-2 py-1 rounded"
                      >
                        <Code className="w-3.5 h-3.5" />
                        <span>{r.sampleRowsJson.length} Rows</span>
                      </button>
                    ) : (
                      <span className="text-xs text-slate-600">—</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Sample Bad Rows Modal */}
      {selectedSampleResult && (
        <div className="fixed inset-0 z-50 bg-slate-950/80 backdrop-blur flex items-center justify-center p-4">
          <div className="bg-slate-900 border border-slate-800 rounded-2xl max-w-3xl w-full max-h-[80vh] flex flex-col shadow-2xl">
            <div className="p-5 border-b border-slate-800 flex items-center justify-between">
              <div>
                <h3 className="text-base font-bold text-white">Sample Discrepancy Rows</h3>
                <p className="text-xs text-slate-400">{selectedSampleResult.ruleId} — {selectedSampleResult.targetTable}</p>
              </div>
              <button
                onClick={() => setSelectedSampleResult(null)}
                className="text-slate-400 hover:text-white text-sm p-1"
              >
                ✕
              </button>
            </div>
            <div className="p-5 overflow-y-auto space-y-3 font-mono text-xs">
              {selectedSampleResult.sampleRowsJson.map((rowJson, idx) => (
                <div key={idx} className="bg-slate-950 border border-slate-800 p-3 rounded-lg text-emerald-300 overflow-x-auto">
                  <pre>{JSON.stringify(JSON.parse(rowJson), null, 2)}</pre>
                </div>
              ))}
            </div>
            <div className="p-4 border-t border-slate-800 flex justify-end">
              <button
                onClick={() => setSelectedSampleResult(null)}
                className="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-semibold rounded-lg"
              >
                Close
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
