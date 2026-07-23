import { mockMachines } from '../store';

export function renderMachinesView(): string {
  return `
    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px;">
      <div>
        <h2 style="font-family: var(--font-heading); font-size: 18px; font-weight: 700;">Machines & Exclusive Resource Groups</h2>
        <p style="font-size: 12px; color: var(--text-muted);">Host machine topology, active agent worker count, and mutually-exclusive resource locks (R-SCH2/R-DM8).</p>
      </div>
      <button class="btn btn-primary">+ Register Machine</button>
    </div>

    <div class="table-container">
      <table class="data-table">
        <thead>
          <tr>
            <th>Machine ID</th>
            <th>Machine Name</th>
            <th>Status</th>
            <th>Active Agents</th>
            <th>Exclusive GPU Locks</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          ${mockMachines.map(m => `
            <tr>
              <td style="font-family: var(--font-code); font-size: 12px; color: var(--accent-cyan);">${m.id}</td>
              <td style="font-weight: 600;">${m.name}</td>
              <td><span class="badge ${m.status === 'Online' ? 'badge-success' : 'badge-warning'}">${m.status}</span></td>
              <td>${m.activeAgents} Worker Threads</td>
              <td style="font-family: var(--font-code); font-size: 12px; color: var(--accent-amber);">${m.gpuLocks}</td>
              <td><button class="btn btn-primary" style="font-size: 11px; padding: 2px 8px;">Inspect</button></td>
            </tr>
          `).join('')}
        </tbody>
      </table>
    </div>
  `;
}
