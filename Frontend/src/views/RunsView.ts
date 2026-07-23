import { mockRuns } from '../store';

export function renderRunsView(): string {
  return `
    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px;">
      <div>
        <h2 style="font-family: var(--font-heading); font-size: 18px; font-weight: 700;">Workflow Runs & Work Queue</h2>
        <p style="font-size: 12px; color: var(--text-muted);">Realtime status of active, queued, and completed runs across all managed projects (R-UI2).</p>
      </div>
      <button class="btn btn-primary">+ Trigger Workflow</button>
    </div>

    <div class="table-container">
      <table class="data-table">
        <thead>
          <tr>
            <th>Run ID</th>
            <th>Workflow Name</th>
            <th>Project Name</th>
            <th>Execution Status</th>
            <th>Started At</th>
            <th>Duration</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          ${mockRuns.map(run => `
            <tr>
              <td style="font-family: var(--font-code); font-size: 12px; color: var(--accent-cyan);">${run.id}</td>
              <td style="font-weight: 600;">${run.workflowName}</td>
              <td>${run.projectName}</td>
              <td>
                <span class="badge ${run.status === 'Passed' ? 'badge-success' : run.status === 'AwaitingApproval' ? 'badge-warning' : 'badge-danger'}">
                  ${run.status}
                </span>
              </td>
              <td style="color: var(--text-muted);">${run.startedAt}</td>
              <td style="font-family: var(--font-code);">${run.duration}</td>
              <td><button class="btn btn-primary" style="font-size: 11px; padding: 2px 8px;">View Logs</button></td>
            </tr>
          `).join('')}
        </tbody>
      </table>
    </div>
  `;
}
