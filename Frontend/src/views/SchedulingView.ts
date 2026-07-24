import { apiClient, ApiError, type ScheduleResponse } from '../api';

export function renderSchedulingView(): string {
  return `
    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px;">
      <div>
        <h2 style="font-family: var(--font-heading); font-size: 18px; font-weight: 700;">Schedule Manager</h2>
        <p style="font-size: 12px; color: var(--text-muted);">Durable cron/interval definitions and serialization rules (R-SCH1/4).</p>
      </div>
      <select id="scheduling-project-select" style="background: rgba(255,255,255,0.03); border: 1px solid var(--border-color); color: var(--text-primary); border-radius: var(--radius-sm); padding: 6px;">
        <option value="">Loading projects…</option>
      </select>
    </div>
    <div id="scheduling-content" class="table-container">
      <div class="card">Select a Project to view its Schedules.</div>
    </div>
  `;
}

export async function wireSchedulingView(): Promise<void> {
  const select = document.getElementById('scheduling-project-select') as HTMLSelectElement | null;
  const content = document.getElementById('scheduling-content');
  if (!select || !content) return;

  try {
    const projects = await apiClient.listProjects();
    if (projects.length === 0) {
      select.innerHTML = '<option value="">No Projects visible</option>';
      content.innerHTML = '<div class="card">No Projects visible to your account yet.</div>';
      return;
    }

    select.innerHTML = projects.map((p) => `<option value="${p.id}">${escapeHtml(p.name)}</option>`).join('');
    select.addEventListener('change', () => loadSchedulesForProject(select.value, content));
    await loadSchedulesForProject(select.value, content);
  } catch (err) {
    select.innerHTML = '<option value="">Error</option>';
    content.innerHTML = `<div class="card" style="color: var(--accent-rose);">Failed to load Projects: ${
      err instanceof ApiError ? err.message : String(err)
    }</div>`;
  }
}

async function loadSchedulesForProject(projectId: string, content: HTMLElement): Promise<void> {
  content.innerHTML = '<div class="card">Loading schedules…</div>';
  try {
    const schedules = await apiClient.listSchedules(projectId);
    content.innerHTML = schedules.length === 0 ? '<div class="card">No Schedules for this Project.</div>' : renderTable(schedules);
  } catch (err) {
    content.innerHTML = `<div class="card" style="color: var(--accent-rose);">Failed to load Schedules: ${
      err instanceof ApiError ? err.message : String(err)
    }</div>`;
  }
}

function renderTable(schedules: ScheduleResponse[]): string {
  return `
    <table class="data-table">
      <thead>
        <tr>
          <th>Schedule ID</th>
          <th>Workflow ID</th>
          <th>Cron</th>
          <th>Serialize Machine</th>
          <th>Serialize Project</th>
          <th>Last Fired</th>
          <th>Status</th>
        </tr>
      </thead>
      <tbody>
        ${schedules.map((s) => `
          <tr>
            <td style="font-family: var(--font-code); color: var(--accent-cyan); font-size: 12px;">${s.id}</td>
            <td style="font-family: var(--font-code); font-size: 12px;">${s.workflowId}</td>
            <td style="font-family: var(--font-code);">${escapeHtml(s.cronExpression)}</td>
            <td><span class="badge ${s.serializePerMachine ? 'badge-success' : 'badge-warning'}">${s.serializePerMachine ? 'Enabled' : 'Disabled'}</span></td>
            <td><span class="badge ${s.serializePerProject ? 'badge-success' : 'badge-warning'}">${s.serializePerProject ? 'Enabled' : 'Disabled'}</span></td>
            <td style="color: var(--text-muted);">${s.lastFiredAtUtc ?? 'Never'}</td>
            <td><span class="badge ${s.isEnabled ? 'badge-primary' : 'badge-warning'}">${s.isEnabled ? 'Active' : 'Disabled'}</span></td>
          </tr>
        `).join('')}
      </tbody>
    </table>
  `;
}

function escapeHtml(value: string): string {
  const div = document.createElement('div');
  div.textContent = value;
  return div.innerHTML;
}
