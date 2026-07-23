import { mockApprovals } from '../store';

export function renderApprovalsView(): string {
  return `
    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px;">
      <div>
        <h2 style="font-family: var(--font-heading); font-size: 18px; font-weight: 700;">Human Approval Queue</h2>
        <p style="font-size: 12px; color: var(--text-muted);">Pending execution approval requests with approver identity attribution and parameter overrides (R-WF5/DM15).</p>
      </div>
      <span class="badge badge-warning" style="font-size: 13px; padding: 6px 12px;">${mockApprovals.length} Pending Actions</span>
    </div>

    <div style="display: flex; flex-direction: column; gap: 16px;">
      ${mockApprovals.map(app => `
        <div class="card" style="border-left: 4px solid var(--accent-amber);">
          <div style="display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 10px;">
            <div>
              <span class="badge badge-warning" style="margin-right: 8px;">${app.gateId}</span>
              <span style="font-weight: 700; font-family: var(--font-heading);">${app.projectName}</span>
            </div>
            <span style="font-size: 11px; color: var(--text-muted);">${app.createdAt}</span>
          </div>

          <p style="font-size: 14px; color: var(--text-primary); margin-bottom: 16px; background: rgba(255,255,255,0.03); padding: 12px; border-radius: var(--radius-sm); border: 1px solid var(--border-color);">
            ${app.prompt}
          </p>

          <div style="display: flex; justify-content: space-between; align-items: center;">
            <span style="font-size: 12px; color: var(--text-muted);">Requested by: <strong>${app.requestedBy}</strong></span>
            <div style="display: flex; gap: 8px;">
              <button class="btn btn-primary" style="background: var(--accent-rose);">Reject</button>
              <button class="btn btn-primary" style="background: var(--accent-emerald);">Approve Execution</button>
            </div>
          </div>
        </div>
      `).join('')}
    </div>
  `;
}
