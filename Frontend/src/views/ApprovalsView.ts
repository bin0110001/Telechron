import { apiClient, ApiError, type ApprovalResponse } from '../api';

export function renderApprovalsView(): string {
  return `
    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px;">
      <div>
        <h2 style="font-family: var(--font-heading); font-size: 18px; font-weight: 700;">Human Approval Queue</h2>
        <p style="font-size: 12px; color: var(--text-muted);">Real pending approval requests (R-WF5/R-DM15). Approving here submits the actual decision.</p>
      </div>
      <span class="badge badge-warning" id="approvals-count-badge" style="font-size: 13px; padding: 6px 12px;">…</span>
    </div>
    <div id="approvals-content" style="display: flex; flex-direction: column; gap: 16px;">
      <div class="card">Loading approvals…</div>
    </div>
  `;
}

export async function wireApprovalsView(): Promise<void> {
  await loadApprovals();
}

async function loadApprovals(): Promise<void> {
  const content = document.getElementById('approvals-content');
  const badge = document.getElementById('approvals-count-badge');
  if (!content) return;

  try {
    const approvals = await apiClient.listPendingApprovals();
    if (badge) badge.textContent = `${approvals.length} Pending Actions`;

    content.innerHTML = approvals.length === 0
      ? '<div class="card">No pending approvals.</div>'
      : approvals.map(renderApprovalCard).join('');

    approvals.forEach((approval) => {
      document.getElementById(`approve-${approval.id}`)?.addEventListener('click', () => submitDecision(approval.id, true));
      document.getElementById(`reject-${approval.id}`)?.addEventListener('click', () => submitDecision(approval.id, false));
    });
  } catch (err) {
    content.innerHTML = `<div class="card" style="color: var(--accent-rose);">Failed to load approvals: ${
      err instanceof ApiError ? err.message : String(err)
    }</div>`;
  }
}

async function submitDecision(requestId: string, approve: boolean): Promise<void> {
  try {
    await apiClient.submitApprovalDecision(requestId, approve);
    await loadApprovals();
  } catch (err) {
    alert(`Failed to submit decision: ${err instanceof ApiError ? err.message : String(err)}`);
  }
}

function renderApprovalCard(app: ApprovalResponse): string {
  return `
    <div class="card" style="border-left: 4px solid var(--accent-amber);">
      <div style="display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 10px;">
        <div>
          <span class="badge badge-warning" style="margin-right: 8px;">${escapeHtml(app.gateId)}</span>
          <span style="font-weight: 700; font-family: var(--font-heading);">${escapeHtml(app.stepId)}</span>
        </div>
        <span style="font-size: 11px; color: var(--text-muted);">${app.createdAtUtc}</span>
      </div>

      <p style="font-size: 14px; color: var(--text-primary); margin-bottom: 16px; background: rgba(255,255,255,0.03); padding: 12px; border-radius: var(--radius-sm); border: 1px solid var(--border-color);">
        ${escapeHtml(app.prompt)}
      </p>

      <div style="display: flex; justify-content: flex-end; align-items: center; gap: 8px;">
        <button class="btn btn-primary" id="reject-${app.id}" style="background: var(--accent-rose);">Reject</button>
        <button class="btn btn-primary" id="approve-${app.id}" style="background: var(--accent-emerald);">Approve</button>
      </div>
    </div>
  `;
}

function escapeHtml(value: string): string {
  const div = document.createElement('div');
  div.textContent = value;
  return div.innerHTML;
}
