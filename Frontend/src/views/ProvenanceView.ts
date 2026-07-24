import { apiClient, ApiError, type RepairAttemptResponse } from '../api';

export function renderProvenanceView(): string {
  return `
    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px;">
      <div>
        <h2 style="font-family: var(--font-heading); font-size: 18px; font-weight: 700;">Repair Provenance & Attestation</h2>
        <p style="font-size: 12px; color: var(--text-muted);">Signed repair-attempt records — "why did this change?" (R-SEC3/R-DM3a).</p>
      </div>
      <select id="provenance-project-select" style="background: rgba(255,255,255,0.03); border: 1px solid var(--border-color); color: var(--text-primary); border-radius: var(--radius-sm); padding: 6px;">
        <option value="">Loading projects…</option>
      </select>
    </div>
    <div id="provenance-content" style="display: flex; flex-direction: column; gap: 16px;">
      <div class="card">Select a Project to view its repair provenance records.</div>
    </div>
  `;
}

export async function wireProvenanceView(): Promise<void> {
  const select = document.getElementById('provenance-project-select') as HTMLSelectElement | null;
  const content = document.getElementById('provenance-content');
  if (!select || !content) return;

  try {
    const projects = await apiClient.listProjects();
    if (projects.length === 0) {
      select.innerHTML = '<option value="">No Projects visible</option>';
      content.innerHTML = '<div class="card">No Projects visible to your account yet.</div>';
      return;
    }

    select.innerHTML = projects.map((p) => `<option value="${p.id}">${escapeHtml(p.name)}</option>`).join('');
    select.addEventListener('change', () => loadAttemptsForProject(select.value, content));
    await loadAttemptsForProject(select.value, content);
  } catch (err) {
    select.innerHTML = '<option value="">Error</option>';
    content.innerHTML = `<div class="card" style="color: var(--accent-rose);">Failed to load Projects: ${
      err instanceof ApiError ? err.message : String(err)
    }</div>`;
  }
}

async function loadAttemptsForProject(projectId: string, content: HTMLElement): Promise<void> {
  content.innerHTML = '<div class="card">Loading repair attempts…</div>';
  try {
    const attempts = await apiClient.listRepairAttempts(projectId);
    content.innerHTML = attempts.length === 0
      ? '<div class="card">No repair attempts recorded for this Project yet.</div>'
      : attempts.map(renderAttemptCard).join('');
  } catch (err) {
    content.innerHTML = `<div class="card" style="color: var(--accent-rose);">Failed to load repair attempts: ${
      err instanceof ApiError ? err.message : String(err)
    }</div>`;
  }
}

function renderAttemptCard(a: RepairAttemptResponse): string {
  const decisionNum = a.approvalDecision === null ? null : typeof a.approvalDecision === 'number' ? a.approvalDecision : Number(a.approvalDecision);
  const decisionLabel = decisionNum === null ? 'Pending' : decisionNum === 0 ? 'Approved' : 'Rejected';
  const decisionBadge = decisionNum === null ? 'badge-warning' : decisionNum === 0 ? 'badge-success' : 'badge-danger';

  return `
    <div class="card">
      <div style="display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 10px;">
        <div style="font-family: var(--font-heading); font-size: 15px; font-weight: 700;">Repair Attempt ${a.id.slice(0, 8)}</div>
        <span class="badge ${decisionBadge}">${decisionLabel}</span>
      </div>
      <div style="font-size: 12px; color: var(--text-secondary); margin-bottom: 8px;">
        <strong>Snapshot:</strong> <span style="font-family: var(--font-code);">${escapeHtml(a.snapshotRef)}</span>
        ${a.commitReference ? ` &nbsp; <strong>Commit:</strong> <span style="font-family: var(--font-code);">${escapeHtml(a.commitReference)}</span>` : ''}
      </div>
      <div style="font-size: 12px; color: var(--text-muted); margin-bottom: 8px;">Resolves ${a.findingIds.length} Finding(s) &nbsp; ${a.createdAtUtc}</div>
      ${a.provenanceRecordJson
        ? `<div style="font-size: 12px; background: rgba(255,255,255,0.03); padding: 12px; border-radius: 6px; font-family: var(--font-code); word-break: break-all;">${escapeHtml(a.provenanceRecordJson)}</div>`
        : '<div style="font-size: 12px; color: var(--text-muted);">No signed provenance record (not yet committed).</div>'}
    </div>
  `;
}

function escapeHtml(value: string): string {
  const div = document.createElement('div');
  div.textContent = value;
  return div.innerHTML;
}
