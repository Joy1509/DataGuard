import React, { useState } from 'react';
import { RuleDefinition } from '../types';
import { Check, X, Search } from 'lucide-react';

interface RulesProps {
  rules: RuleDefinition[];
}

export const Rules: React.FC<RulesProps> = ({ rules }) => {
  const [searchTerm, setSearchTerm] = useState('');

  const filteredRules = rules.filter(r =>
    r.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
    r.id.toLowerCase().includes(searchTerm.toLowerCase()) ||
    r.type.toLowerCase().includes(searchTerm.toLowerCase()) ||
    r.targetTable.toLowerCase().includes(searchTerm.toLowerCase())
  );

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold text-white">Validation Rules Catalog</h1>
          <p className="text-xs text-slate-400 mt-0.5">Active data quality and migration rules</p>
        </div>

        <div className="flex items-center space-x-3 w-72">
          <div className="bg-slate-900 border border-slate-800 rounded-lg px-3 py-1.5 flex items-center space-x-2 w-full">
            <Search className="w-4 h-4 text-slate-500" />
            <input
              type="text"
              placeholder="Search rules..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              className="bg-transparent border-none text-xs text-slate-200 placeholder-slate-500 focus:outline-none w-full"
            />
          </div>
        </div>
      </div>

      {/* Rules Table */}
      <div className="bg-slate-900/60 border border-slate-800 rounded-xl overflow-hidden backdrop-blur">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead className="bg-slate-950/60 text-xs uppercase text-slate-400 font-semibold border-b border-slate-800">
              <tr>
                <th className="px-6 py-3">Status</th>
                <th className="px-6 py-3">Rule ID</th>
                <th className="px-6 py-3">Name</th>
                <th className="px-6 py-3">Type</th>
                <th className="px-6 py-3">Severity</th>
                <th className="px-6 py-3">Source Table</th>
                <th className="px-6 py-3">Target Table</th>
                <th className="px-6 py-3 text-right">Timeout</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800">
              {filteredRules.map(r => (
                <tr key={r.id} className="hover:bg-slate-800/40 transition-colors">
                  <td className="px-6 py-4">
                    <span className={`inline-flex items-center space-x-1 px-2.5 py-0.5 rounded-full text-xs font-semibold ${
                      r.enabled ? 'bg-emerald-500/10 text-emerald-400 border border-emerald-500/20' : 'bg-slate-800 text-slate-500'
                    }`}>
                      {r.enabled ? <Check className="w-3 h-3" /> : <X className="w-3 h-3" />}
                      <span>{r.enabled ? 'Enabled' : 'Disabled'}</span>
                    </span>
                  </td>
                  <td className="px-6 py-4 font-mono text-xs font-semibold text-slate-300">{r.id}</td>
                  <td className="px-6 py-4 text-slate-200 font-medium">{r.name}</td>
                  <td className="px-6 py-4 font-mono text-xs text-sky-400">{r.type}</td>
                  <td className="px-6 py-4">
                    <span className={`text-xs font-semibold px-2 py-0.5 rounded ${
                      r.severity === 'Critical' ? 'bg-rose-500/10 text-rose-400 border border-rose-500/20' :
                      r.severity === 'High' ? 'bg-amber-500/10 text-amber-400 border border-amber-500/20' :
                      'bg-yellow-500/10 text-yellow-400 border border-yellow-500/20'
                    }`}>
                      {r.severity}
                    </span>
                  </td>
                  <td className="px-6 py-4 font-mono text-xs text-slate-400">{r.sourceTable || '—'}</td>
                  <td className="px-6 py-4 font-mono text-xs text-slate-300">{r.targetTable}</td>
                  <td className="px-6 py-4 text-right font-mono text-xs text-slate-400">{r.timeoutSeconds}s</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};
