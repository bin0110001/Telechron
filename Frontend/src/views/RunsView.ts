import { apiClient, ApiError, type RunResponse } from '../api';

const RUN_STATUS_LABELS: Record<number, string> = {
  0: 'Pending', 1: 'Running', 2: 'Passed', 3: 'Failed', 4: 'Cancelled', 5: 'TimedOut', 6: 'Stalled',
};

export function renderRunsView(): string {
  return `
    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px;">
      <div>
        <h2 style="font-family: var(--font-heading); font-size: 18px; font-weight: 700;">Workflow Runs</h2>
        <p style="font-size: 12px; color: var(--text-muted);">Real execution status of Runs for the selected Project (R-UI2).</p>
      </div>
      <select id="runs-project-select" style="background: rgba(255,255,255,0.03); border: 1px solid var(--border-color); color: var(--text-primary); border-radius: var(--radius-sm); padding: 6px;">
        <option value="">Loading projects…</option>
      </select>
    </div>
    <div id="runs-content" class="table-container">
      <div class="card">Select a Project to view its Runs.</div>
    </div>
  `;
}

export async function wireRunsView(): Promise<void> {
  const select = document.getElementById('runs-project-select') as HTMLSelectElement | null;
  const content = document.getElementById('runs-content');
  if (!select || !content) return;

  try {
    const projects = await apiClient.listProjects();
    if (projects.length === 0) {
      select.innerHTML = '<option value="">No Projects visible</option>';
      content.innerHTML = '<div class="card">No Projects visible to your account yet.</div>';
      return;
    }

    select.innerHTML = projects.map((p) => `<option value="${p.id}">${escapeHtml(p.name)}</option>`).join('');
    select.addEventListener('change', () => loadRunsForProject(select.value, content));
    await loadRunsForProject(select.value, content);
  } catch (err) {
    select.innerHTML = '<option value="">Error</option>';
    content.innerHTML = `<div class="card" style="color: var(--accent-rose);">Failed to load Projects: ${
      err instanceof ApiError ? err.message : String(err)
    }</div>`;
  }
}

async function loadRunsForProject(projectId: string, content: HTMLElement): Promise<void> {
  content.innerHTML = '<div class="card">Loading runs…</div>';
  try {
    const runs = await apiClient.listRuns(projectId);
    content.innerHTML = runs.length === 0 ? '<div class="card">No Runs yet for this Project.</div>' : renderRunsTable(runs);
  } catch (err) {
    content.innerHTML = `<div class="card" style="color: var(--accent-rose);">Failed to load Runs: ${
      err instanceof ApiError ? err.message : String(err)
    }</div>`;
  }
}

function renderRunsTable(runs: RunResponse[]): string {
  return `
    <table class="data-table">
      <thead>
        <tr>
          <th>Run ID</th>
          <th>Status</th>
          <th>Started</th>
          <th>Completed</th>
          <th>Last Heartbeat</th>
        </tr>
      </thead>
      <tbody>
        ${runs.map((run) => {
          const statusNum = typeof run.status === 'number' ? run.status : Number(run.status);
          const statusLabel = RUN_STATUS_LABELS[statusNum] ?? String(run.status);
          const badgeClass = statusLabel === 'Passed' ? 'badge-success'
            : statusLabel === 'Failed' || statusLabel === 'Stalled' || statusLabel === 'TimedOut' ? 'badge-danger'
            : 'badge-warning';
          return `
            <tr>
              <td style="font-family: var(--font-code); font-size: 12px; color: var(--accent-cyan);">${run.id}</td>
              <td><span class="badge ${badgeClass}">${statusLabel}</span></td>
              <td style="color: var(--text-muted);">${run.startedAtUtc ?? '—'}</td>
              <td style="color: var(--text-muted);">${run.completedAtUtc ?? '—'}</td>
              <td style="color: var(--text-muted);">${run.lastHeartbeatUtc ?? '—'}</td>
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
