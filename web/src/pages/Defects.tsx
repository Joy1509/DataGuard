import React, { useState } from 'react';
import { Defect } from '../types';
import { RotateCcw, Filter } from 'lucide-react';

interface DefectsProps {
  defects: Defect[];
}

export const Defects: React.FC<DefectsProps> = ({ defects }) => {
  const [activeTab, setActiveTab] = useState<string>('ALL');
  const [severityFilter, setSeverityFilter] = useState<string>('ALL');

  const filteredDefects = defects.filter(d => {
    const matchesTab = activeTab === 'ALL' || d.status === activeTab;
    const matchesSeverity = severityFilter === 'ALL' || d.severity === severityFilter;
    return matchesTab && matchesSeverity;
  });

  const counts = {
    all: defects.length,
    open: defects.filter(d => d.status === 'Open').length,
    acknowledged: defects.filter(d => d.status === 'Acknowledged').length,
    reopened: defects.filter(d => d.status === 'Reopened').length,
    resolved: defects.filter(d => d.status === 'Resolved').length,
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold text-white">Defect Tracker & State Lifecycle</h1>
          <p className="text-xs text-slate-400 mt-0.5">Automated run-over-run de-duplicated defect management</p>
        </div>

        <div className="flex items-center space-x-3">
          <Filter className="w-4 h-4 text-slate-500" />
          <select
            value={severityFilter}
            onChange={(e) => setSeverityFilter(e.target.value)}
            className="bg-slate-900 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-slate-300 focus:outline-none"
          >
            <option value="ALL">All Severities</option>
            <option value="Critical">Critical</option>
            <option value="High">High</option>
            <option value="Medium">Medium</option>
            <option value="Low">Low</option>
          </select>
        </div>
      </div>

      {/* Status Tabs */}
      <div className="flex space-x-2 border-b border-slate-800 pb-2">
        {[
          { id: 'ALL', label: 'All Defects', count: counts.all },
          { id: 'Open', label: 'Open', count: counts.open },
          { id: 'Acknowledged', label: 'Acknowledged', count: counts.acknowledged },
          { id: 'Reopened', label: 'Reopened (Regressions)', count: counts.reopened },
          { id: 'Resolved', label: 'Resolved', count: counts.resolved },
        ].map(tab => (
          <button
            key={tab.id}
            onClick={() => setActiveTab(tab.id)}
            className={`px-3.5 py-1.5 rounded-lg text-xs font-semibold flex items-center space-x-2 transition-colors ${
              activeTab === tab.id
                ? 'bg-sky-500/10 text-sky-400 border border-sky-500/20'
                : 'text-slate-400 hover:text-slate-200 hover:bg-slate-900'
            }`}
          >
            <span>{tab.label}</span>
            <span className="bg-slate-800 px-2 py-0.5 rounded-full text-[10px] text-slate-300">{tab.count}</span>
          </button>
        ))}
      </div>

      {/* Defects List */}
      <div className="grid grid-cols-1 gap-4">
        {filteredDefects.length > 0 ? (
          filteredDefects.map(d => (
            <div key={d.defectId} className="bg-slate-900/60 border border-slate-800 rounded-xl p-5 backdrop-blur flex flex-col md:flex-row md:items-center justify-between gap-4">
              <div className="space-y-2">
                <div className="flex items-center space-x-3">
                  <span className={`px-2.5 py-0.5 rounded-full text-xs font-semibold ${
                    d.status === 'Resolved' ? 'bg-emerald-500/10 text-emerald-400 border border-emerald-500/20' :
                    d.status === 'Reopened' ? 'bg-amber-500/10 text-amber-400 border border-amber-500/20' :
                    'bg-rose-500/10 text-rose-400 border border-rose-500/20'
                  }`}>
                    {d.status}
                  </span>

                  <span className={`text-xs font-semibold px-2 py-0.5 rounded ${
                    d.severity === 'Critical' ? 'bg-rose-500/10 text-rose-400 border border-rose-500/20' :
                    d.severity === 'High' ? 'bg-amber-500/10 text-amber-400 border border-amber-500/20' :
                    'bg-yellow-500/10 text-yellow-400 border border-yellow-500/20'
                  }`}>
                    {d.severity}
                  </span>

                  <span className="font-mono text-xs font-bold text-white">{d.ruleId}</span>
                </div>

                <div className="text-sm font-medium text-slate-200">
                  Target: <span className="font-mono text-sky-400">{d.targetTable}</span>
                  {d.targetColumn && <> • Column: <span className="font-mono text-sky-400">{d.targetColumn}</span></>}
                </div>

                <div className="flex items-center space-x-4 text-xs text-slate-400">
                  <span>Fingerprint: <code className="text-slate-300">{d.fingerprint.substring(0, 16)}...</code></span>
                  <span>Occurrences: <strong className="text-slate-200">{d.occurrenceCount}</strong></span>
                  {d.regressionCount > 0 && (
                    <span className="text-amber-400 font-semibold flex items-center space-x-1">
                      <RotateCcw className="w-3 h-3" />
                      <span>{d.regressionCount} Regressions</span>
                    </span>
                  )}
                </div>
              </div>

              <div className="text-right text-xs text-slate-400 space-y-1">
                <div>First Seen: {new Date(d.firstSeenAtUtc).toLocaleDateString()}</div>
                <div>Last Seen: {new Date(d.lastSeenAtUtc).toLocaleDateString()}</div>
                {d.resolvedAtUtc && <div className="text-emerald-400">Resolved: {new Date(d.resolvedAtUtc).toLocaleDateString()}</div>}
              </div>
            </div>
          ))
        ) : (
          <div className="bg-slate-900/40 border border-slate-800 rounded-xl p-12 text-center text-slate-500">
            No defects found matching current filters.
          </div>
        )}
      </div>
    </div>
  );
};
