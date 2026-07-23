import { mockRequirements } from '../store';

export function renderDesignDocView(): string {
  return `
    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px;">
      <div>
        <h2 style="font-family: var(--font-heading); font-size: 18px; font-weight: 700;">Design Document & Requirement Manager</h2>
        <p style="font-size: 12px; color: var(--text-muted);">Living requirement specifications, revision history, edit diff proposer, and drift markers (R-DM16).</p>
      </div>
      <button class="btn btn-primary">+ Propose Requirement Edit</button>
    </div>

    <div class="table-container">
      <table class="data-table">
        <thead>
          <tr>
            <th>Requirement ID</th>
            <th>Requirement Title</th>
            <th>Statement</th>
            <th>Status</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          ${mockRequirements.map(req => `
            <tr>
              <td style="font-family: var(--font-code); font-weight: 700; color: var(--accent-cyan);">${req.requirementId}</td>
              <td style="font-weight: 600;">${req.title}</td>
              <td style="color: var(--text-secondary); max-width: 400px;">${req.statement}</td>
              <td>
                <span class="badge ${req.status === 'Active' ? 'badge-success' : req.status === 'UnderRevision' ? 'badge-warning' : 'badge-danger'}">
                  ${req.status}
                </span>
              </td>
              <td><button class="btn btn-primary" style="font-size: 11px; padding: 2px 8px;">View Revision History</button></td>
            </tr>
          `).join('')}
        </tbody>
      </table>
    </div>
  `;
}
