import { apiClient, ApiError, type MachineResponse } from '../api';

export function renderMachinesView(): string {
  return `
    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px;">
      <div>
        <h2 style="font-family: var(--font-heading); font-size: 18px; font-weight: 700;">Machines</h2>
        <p style="font-size: 12px; color: var(--text-muted);">Registered Agent-hosting machines and their online status (R-SCH3/R-DM8).</p>
      </div>
    </div>
    <div id="machines-content" class="table-container">
      <div class="card">Loading machines…</div>
    </div>
  `;
}

export async function wireMachinesView(): Promise<void> {
  const content = document.getElementById('machines-content');
  if (!content) return;

  try {
    const machines = await apiClient.listMachines();
    content.innerHTML = machines.length === 0 ? '<div class="card">No Machines registered yet.</div>' : renderTable(machines);
  } catch (err) {
    content.innerHTML = `<div class="card" style="color: var(--accent-rose);">Failed to load Machines: ${
      err instanceof ApiError ? err.message : String(err)
    }</div>`;
  }
}

function renderTable(machines: MachineResponse[]): string {
  return `
    <table class="data-table">
      <thead>
        <tr>
          <th>Machine ID</th>
          <th>Name</th>
          <th>Hostname</th>
          <th>Status</th>
          <th>Registered</th>
          <th>Last Heartbeat</th>
        </tr>
      </thead>
      <tbody>
        ${machines.map((m) => `
          <tr>
            <td style="font-family: var(--font-code); font-size: 12px; color: var(--accent-cyan);">${m.id}</td>
            <td style="font-weight: 600;">${escapeHtml(m.name)}</td>
            <td>${escapeHtml(m.hostname)}</td>
            <td><span class="badge ${m.isOnline ? 'badge-success' : 'badge-warning'}">${m.isOnline ? 'Online' : 'Offline'}</span></td>
            <td style="color: var(--text-muted);">${m.registeredAtUtc}</td>
            <td style="color: var(--text-muted);">${m.lastHeartbeatUtc ?? '—'}</td>
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
