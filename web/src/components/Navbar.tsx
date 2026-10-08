import React from 'react';
import { ShieldCheck, Activity, AlertTriangle, Layers, Play } from 'lucide-react';

interface NavbarProps {
  activeTab: string;
  onTabChange: (tab: string) => void;
  onTriggerRun: () => void;
  isTriggering: boolean;
}

export const Navbar: React.FC<NavbarProps> = ({ activeTab, onTabChange, onTriggerRun, isTriggering }) => {
  const tabs = [
    { id: 'overview', label: 'Overview', icon: Activity },
    { id: 'runs', label: 'Runs & Results', icon: ShieldCheck },
    { id: 'defects', label: 'Defect Tracker', icon: AlertTriangle },
    { id: 'rules', label: 'Rules Catalog', icon: Layers },
  ];

  return (
    <header className="border-b border-slate-800 bg-slate-900/80 backdrop-blur sticky top-0 z-50">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 h-16 flex items-center justify-between">
        <div className="flex items-center space-x-3">
          <div className="bg-sky-500/10 p-2 rounded-xl border border-sky-500/20 text-sky-400">
            <ShieldCheck className="w-6 h-6" />
          </div>
          <div>
            <div className="flex items-center space-x-2">
              <span className="font-bold text-lg tracking-tight text-white">DataGuard</span>
              <span className="text-xs font-mono bg-sky-500/10 text-sky-400 border border-sky-500/20 px-2 py-0.5 rounded-full">v1.0.0</span>
            </div>
            <p className="text-xs text-slate-400">SQL Server Migration Verification Engine</p>
          </div>
        </div>

        <nav className="flex items-center space-x-1">
          {tabs.map(tab => {
            const Icon = tab.icon;
            const isActive = activeTab === tab.id;
            return (
              <button
                key={tab.id}
                onClick={() => onTabChange(tab.id)}
                className={`flex items-center space-x-2 px-3.5 py-2 rounded-lg text-sm font-medium transition-colors ${
                  isActive
                    ? 'bg-sky-500/10 text-sky-400 border border-sky-500/20'
                    : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800/60'
                }`}
              >
                <Icon className="w-4 h-4" />
                <span>{tab.label}</span>
              </button>
            );
          })}
        </nav>

        <div className="flex items-center space-x-3">
          <button
            onClick={onTriggerRun}
            disabled={isTriggering}
            className="flex items-center space-x-2 bg-sky-500 hover:bg-sky-600 disabled:opacity-50 text-white font-semibold text-sm px-4 py-2 rounded-lg shadow-lg shadow-sky-500/20 transition-all active:scale-95"
          >
            <Play className={`w-4 h-4 ${isTriggering ? 'animate-spin' : ''}`} />
            <span>{isTriggering ? 'Running...' : 'Run Validation'}</span>
          </button>
        </div>
      </div>
    </header>
  );
};
