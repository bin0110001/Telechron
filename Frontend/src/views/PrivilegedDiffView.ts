import { apiClient, ApiError, type PendingRepairDiffResponse } from '../api';

export function renderPrivilegedDiffView(): string {
  return `
    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px;">
      <div>
        <h2 style="font-family: var(--font-heading); font-size: 18px; font-weight: 700;">Privileged Diff Review</h2>
        <p style="font-size: 12px; color: var(--text-muted);">Repair patches still awaiting a human decision (R-SEC4/R-FIX12/13).</p>
      </div>
      <select id="privdiff-project-select" style="background: rgba(255,255,255,0.03); border: 1px solid var(--border-color); color: var(--text-primary); border-radius: var(--radius-sm); padding: 6px;">
        <option value="">Loading projects…</option>
      </select>
    </div>
    <div id="privdiff-content" style="display: flex; flex-direction: column; gap: 16px;">
      <div class="card">Select a Project to view pending repair diffs.</div>
    </div>
  `;
}

export async function wirePrivilegedDiffView(): Promise<void> {
  const select = document.getElementById('privdiff-project-select') as HTMLSelectElement | null;
  const content = document.getElementById('privdiff-content');
  if (!select || !content) return;

  try {
    const projects = await apiClient.listProjects();
    if (projects.length === 0) {
      select.innerHTML = '<option value="">No Projects visible</option>';
      content.innerHTML = '<div class="card">No Projects visible to your account yet.</div>';
      return;
    }

    select.innerHTML = projects.map((p) => `<option value="${p.id}">${escapeHtml(p.name)}</option>`).join('');
    select.addEventListener('change', () => loadDiffsForProject(select.value, content));
    await loadDiffsForProject(select.value, content);
  } catch (err) {
    select.innerHTML = '<option value="">Error</option>';
    content.innerHTML = `<div class="card" style="color: var(--accent-rose);">Failed to load Projects: ${
      err instanceof ApiError ? err.message : String(err)
    }</div>`;
  }
}

async function loadDiffsForProject(projectId: string, content: HTMLElement): Promise<void> {
  content.innerHTML = '<div class="card">Loading pending diffs…</div>';
  try {
    const diffs = await apiClient.listPendingRepairDiffs(projectId);
    content.innerHTML = diffs.length === 0
      ? '<div class="card">No repair patches awaiting review for this Project.</div>'
      : diffs.map(renderDiffCard).join('');
  } catch (err) {
    content.innerHTML = `<div class="card" style="color: var(--accent-rose);">Failed to load pending diffs: ${
      err instanceof ApiError ? err.message : String(err)
    }</div>`;
  }
}

function renderDiffCard(diff: PendingRepairDiffResponse): string {
  return `
    <div class="card" style="font-family: var(--font-code); font-size: 12px; background: #080a0e; overflow-x: auto;">
      <div style="padding: 8px 12px; border-bottom: 1px solid var(--border-color); color: var(--text-muted); font-size: 11px;">
        Repair Attempt ${diff.id.slice(0, 8)} &nbsp; Snapshot: ${escapeHtml(diff.snapshotRef)} &nbsp; ${diff.createdAtUtc}
      </div>
      <pre style="margin: 0; padding: 16px; line-height: 1.6; white-space: pre-wrap; word-break: break-word;">${escapeHtml(diff.patchDiff || '(empty patch)')}</pre>
    </div>
  `;
}

function escapeHtml(value: string): string {
  const div = document.createElement('div');
  div.textContent = value;
  return div.innerHTML;
}
