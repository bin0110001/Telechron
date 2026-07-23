import { mockAuditLogs } from '../store';

export function renderAuditLogView(): string {
  return `
    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px;">
      <div>
        <h2 style="font-family: var(--font-heading); font-size: 18px; font-weight: 700;">Immutable System Audit Log</h2>
        <p style="font-size: 12px; color: var(--text-muted);">Audit history log with trace ID filtering and correlation context (R-SEC7/R-REL6).</p>
      </div>
      <input type="text" placeholder="Filter by Trace ID..." class="prompt-input" style="background: rgba(255,255,255,0.05); padding: 8px 12px; border-radius: 6px; width: 220px; font-size: 12px; border: 1px solid var(--border-color);" />
    </div>

    <div class="table-container">
      <table class="data-table">
        <thead>
          <tr>
            <th>Log ID</th>
            <th>Timestamp (UTC)</th>
            <th>Correlation Trace ID</th>
            <th>Action</th>
            <th>User / Agent</th>
          </tr>
        </thead>
        <tbody>
          ${mockAuditLogs.map(log => `
            <tr>
              <td style="font-family: var(--font-code); font-size: 12px;">${log.id}</td>
              <td style="color: var(--text-muted); font-size: 12px;">${log.timestamp}</td>
              <td style="font-family: var(--font-code); color: var(--accent-cyan);">${log.traceId}</td>
              <td><span class="badge badge-primary">${log.action}</span></td>
              <td style="font-weight: 600;">${log.userOrAgent}</td>
            </tr>
          `).join('')}
        </tbody>
      </table>
    </div>
  `;
}
