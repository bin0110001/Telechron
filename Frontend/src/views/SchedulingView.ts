export function renderSchedulingView(): string {
  return `
    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px;">
      <div>
        <h2 style="font-family: var(--font-heading); font-size: 18px; font-weight: 700;">Durable Workflow Schedule Manager</h2>
        <p style="font-size: 12px; color: var(--text-muted);">Cron and interval definitions, project and machine execution serialization toggles (R-SCH1/4).</p>
      </div>
      <button class="btn btn-primary">+ Create Schedule</button>
    </div>

    <div class="table-container">
      <table class="data-table">
        <thead>
          <tr>
            <th>Schedule ID</th>
            <th>Target Workflow</th>
            <th>Cron Rule</th>
            <th>Serialize Machine</th>
            <th>Serialize Project</th>
            <th>Last Fired</th>
            <th>Status</th>
          </tr>
        </thead>
        <tbody>
          <tr>
            <td style="font-family: var(--font-code); color: var(--accent-cyan);">sched_nightly_audit</td>
            <td style="font-weight: 600;">Scheduled Dependency Audit</td>
            <td style="font-family: var(--font-code);">0 0 * * *</td>
            <td><span class="badge badge-success">Enabled</span></td>
            <td><span class="badge badge-success">Enabled</span></td>
            <td style="color: var(--text-muted);">12 mins ago</td>
            <td><span class="badge badge-primary">Active</span></td>
          </tr>
        </tbody>
      </table>
    </div>
  `;
}
