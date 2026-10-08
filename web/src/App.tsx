import React, { useState, useEffect } from 'react';
import { Navbar } from './components/Navbar';
import { Overview } from './pages/Overview';
import { RunDetail } from './pages/RunDetail';
import { Defects } from './pages/Defects';
import { Rules } from './pages/Rules';
import { RunSummary, Defect, RuleDefinition } from './types';
import { fetchRuns, fetchRunDetail, fetchDefects, fetchRules, triggerRun } from './api/client';

export const App: React.FC = () => {
  const [activeTab, setActiveTab] = useState('overview');
  const [runs, setRuns] = useState<RunSummary[]>([]);
  const [defects, setDefects] = useState<Defect[]>([]);
  const [rules, setRules] = useState<RuleDefinition[]>([]);
  const [selectedRun, setSelectedRun] = useState<RunSummary | null>(null);
  const [isTriggering, setIsTriggering] = useState(false);
  const [loading, setLoading] = useState(true);

  const loadData = async () => {
    try {
      const [runsData, defectsData, rulesData] = await Promise.all([
        fetchRuns().catch(() => []),
        fetchDefects().catch(() => []),
        fetchRules().catch(() => []),
      ]);
      setRuns(runsData);
      setDefects(defectsData);
      setRules(rulesData);
    } catch (err) {
      console.error('Failed loading data', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
    const interval = setInterval(loadData, 5000);
    return () => clearInterval(interval);
  }, []);

  const handleSelectRun = async (runId: string) => {
    try {
      const detail = await fetchRunDetail(runId);
      setSelectedRun(detail);
      setActiveTab('runDetail');
    } catch (err) {
      console.error('Failed fetching run detail', err);
    }
  };

  const handleTriggerRun = async () => {
    setIsTriggering(true);
    try {
      await triggerRun({ rulesPath: 'rules.sample.json', parallelism: 4 });
      await loadData();
    } catch (err) {
      console.error('Trigger run failed', err);
    } finally {
      setIsTriggering(false);
    }
  };

  return (
    <div className="min-h-screen bg-slate-950 flex flex-col">
      <Navbar
        activeTab={activeTab}
        onTabChange={(tab) => {
          setActiveTab(tab);
          if (tab !== 'runDetail') setSelectedRun(null);
        }}
        onTriggerRun={handleTriggerRun}
        isTriggering={isTriggering}
      />

      <main className="flex-1 max-w-7xl w-full mx-auto px-4 sm:px-6 lg:px-8 py-8">
        {loading && runs.length === 0 ? (
          <div className="flex items-center justify-center h-64 text-slate-400 text-sm">
            Connecting to DataGuard API...
          </div>
        ) : (
          <>
            {activeTab === 'overview' && (
              <Overview runs={runs} defects={defects} onSelectRun={handleSelectRun} />
            )}

            {(activeTab === 'runs' || activeTab === 'runDetail') && selectedRun && (
              <RunDetail run={selectedRun} onBack={() => setActiveTab('overview')} />
            )}

            {activeTab === 'runs' && !selectedRun && (
              <Overview runs={runs} defects={defects} onSelectRun={handleSelectRun} />
            )}

            {activeTab === 'defects' && <Defects defects={defects} />}

            {activeTab === 'rules' && <Rules rules={rules} />}
          </>
        )}
      </main>
    </div>
  );
};

export default App;
