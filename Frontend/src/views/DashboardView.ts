import { apiClient, ApiError, type RunResponse } from '../api';

export function renderDashboardView(): string {
  return `
    <div id="dashboard-metrics" class="grid-cards">
      <div class="card">Loading dashboard…</div>
    </div>
    <div class="table-container" style="margin-top: 24px;">
      <div style="padding: 16px 18px; font-family: var(--font-heading); font-weight: 700; font-size: 15px; border-bottom: 1px solid var(--border-color);">
        RECENT WORKFLOW EXECUTION RUNS
      </div>
      <div id="dashboard-runs-content">
        <div class="card" style="border: none;">Loading runs…</div>
      </div>
    </div>
  `;
}

const RUN_STATUS_LABELS: Record<number, string> = {
  0: 'Pending', 1: 'Running', 2: 'Passed', 3: 'Failed', 4: 'Cancelled', 5: 'TimedOut', 6: 'Stalled',
};

export async function wireDashboardView(): Promise<void> {
  const metrics = document.getElementById('dashboard-metrics');
  const runsContent = document.getElementById('dashboard-runs-content');
  if (!metrics || !runsContent) return;

  try {
    const [projects, machines, pendingApprovals] = await Promise.all([
      apiClient.listProjects(),
      apiClient.listMachines(),
      apiClient.listPendingApprovals(),
    ]);

    const runsPerProject = await Promise.all(projects.map((p) => apiClient.listRuns(p.id).catch(() => [] as RunResponse[])));
    const allRuns = runsPerProject.flat();
    const projectNameById = new Map(projects.map((p) => [p.id, p.name]));

    const activeRuns = allRuns.filter((r) => Number(r.status) === 0 || Number(r.status) === 1);
    const onlineMachines = machines.filter((m) => m.isOnline);

    metrics.innerHTML = `
      <div class="card">
        <div class="card-title">ACTIVE RUNS <span class="badge badge-primary">Live</span></div>
        <div class="card-metric">${activeRuns.length} Active</div>
        <div style="font-size: 12px; color: var(--text-muted); margin-top: 6px;">Total Runs (visible Projects): ${allRuns.length}</div>
      </div>
      <div class="card">
        <div class="card-title">PENDING APPROVAL GATES <span class="badge badge-warning">Action Required</span></div>
        <div class="card-metric" style="color: var(--accent-amber);">${pendingApprovals.length} Gates</div>
        <div style="font-size: 12px; color: var(--text-muted); margin-top: 6px;">Privileged paths & synthesis requests</div>
      </div>
      <div class="card">
        <div class="card-title">VISIBLE PROJECTS <span class="badge badge-primary">Scope</span></div>
        <div class="card-metric">${projects.length}</div>
        <div style="font-size: 12px; color: var(--text-muted); margin-top: 6px;">Owned or member-of Projects</div>
      </div>
      <div class="card">
        <div class="card-title">ONLINE MACHINES <span class="badge badge-primary">Cluster</span></div>
        <div class="card-metric">${onlineMachines.length} / ${machines.length}</div>
        <div style="font-size: 12px; color: var(--text-muted); margin-top: 6px;">Registered Agent-hosting machines</div>
      </div>
    `;

    const recentRuns = allRuns
      .slice()
      .sort((a, b) => (b.startedAtUtc ?? '').localeCompare(a.startedAtUtc ?? ''))
      .slice(0, 10);

    runsContent.innerHTML = recentRuns.length === 0
      ? '<div class="card" style="border: none;">No Runs recorded yet across your visible Projects.</div>'
      : renderRunsTable(recentRuns, projectNameById);
  } catch (err) {
    metrics.innerHTML = `<div class="card" style="color: var(--accent-rose);">Failed to load dashboard: ${
      err instanceof ApiError ? err.message : String(err)
    }</div>`;
    runsContent.innerHTML = '';
  }
}

function renderRunsTable(runs: RunResponse[], projectNameById: Map<string, string>): string {
  return `
    <table class="data-table">
      <thead>
        <tr>
          <th>Run ID</th>
          <th>Project</th>
          <th>Status</th>
          <th>Started</th>
        </tr>
      </thead>
      <tbody>
        ${runs.map((run) => {
          const statusLabel = RUN_STATUS_LABELS[Number(run.status)] ?? String(run.status);
          const badgeClass = statusLabel === 'Passed' ? 'badge-success'
            : statusLabel === 'Failed' || statusLabel === 'Stalled' || statusLabel === 'TimedOut' ? 'badge-danger'
            : 'badge-warning';
          return `
            <tr>
              <td style="font-family: var(--font-code); font-size: 12px; color: var(--accent-cyan);">${run.id}</td>
              <td>${escapeHtml(projectNameById.get(run.projectId) ?? run.projectId)}</td>
              <td><span class="badge ${badgeClass}">${statusLabel}</span></td>
              <td style="color: var(--text-muted);">${run.startedAtUtc ?? '—'}</td>
            </tr>
          `;
        }).join('')}
      </tbody>
    </table>
  `;
}

function escapeHtml(value: string): string {
  const div = document.createElement('div');
  div.textContent = value;
  return div.innerHTML;
}
