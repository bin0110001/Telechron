import { apiClient, ApiError, type RequirementResponse } from '../api';

const REQUIREMENT_STATUS_LABELS: Record<number, string> = {
  0: 'Active', 1: 'Superseded', 2: 'Deprecated',
};

export function renderDesignDocView(): string {
  return `
    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px;">
      <div>
        <h2 style="font-family: var(--font-heading); font-size: 18px; font-weight: 700;">Design Document</h2>
        <p style="font-size: 12px; color: var(--text-muted);">A Project's living Requirements (R-DM16).</p>
      </div>
      <select id="designdoc-project-select" style="background: rgba(255,255,255,0.03); border: 1px solid var(--border-color); color: var(--text-primary); border-radius: var(--radius-sm); padding: 6px;">
        <option value="">Loading projects…</option>
      </select>
    </div>
    <div id="designdoc-content" class="table-container">
      <div class="card">Select a Project to view its Design Document.</div>
    </div>
  `;
}

export async function wireDesignDocView(): Promise<void> {
  const select = document.getElementById('designdoc-project-select') as HTMLSelectElement | null;
  const content = document.getElementById('designdoc-content');
  if (!select || !content) return;

  try {
    const projects = await apiClient.listProjects();
    if (projects.length === 0) {
      select.innerHTML = '<option value="">No Projects visible</option>';
      content.innerHTML = '<div class="card">No Projects visible to your account yet.</div>';
      return;
    }

    select.innerHTML = projects.map((p) => `<option value="${p.id}">${escapeHtml(p.name)}</option>`).join('');
    select.addEventListener('change', () => loadDesignDocForProject(select.value, content));
    await loadDesignDocForProject(select.value, content);
  } catch (err) {
    select.innerHTML = '<option value="">Error</option>';
    content.innerHTML = `<div class="card" style="color: var(--accent-rose);">Failed to load Projects: ${
      err instanceof ApiError ? err.message : String(err)
    }</div>`;
  }
}

async function loadDesignDocForProject(projectId: string, content: HTMLElement): Promise<void> {
  content.innerHTML = '<div class="card">Loading design document…</div>';
  try {
    const doc = await apiClient.getDesignDocument(projectId);
    if (doc === null) {
      content.innerHTML = '<div class="card">This Project has no Design Document yet.</div>';
      return;
    }
    content.innerHTML = doc.requirements.length === 0
      ? '<div class="card">Design Document has no Requirements yet.</div>'
      : renderRequirementsTable(doc.requirements);
  } catch (err) {
    content.innerHTML = `<div class="card" style="color: var(--accent-rose);">Failed to load Design Document: ${
      err instanceof ApiError ? err.message : String(err)
    }</div>`;
  }
}

function renderRequirementsTable(requirements: RequirementResponse[]): string {
  return `
    <table class="data-table">
      <thead>
        <tr>
          <th>Requirement ID</th>
          <th>Title</th>
          <th>Body</th>
          <th>Status</th>
        </tr>
      </thead>
      <tbody>
        ${requirements.map((req) => {
          const statusNum = typeof req.status === 'number' ? req.status : Number(req.status);
          const statusLabel = REQUIREMENT_STATUS_LABELS[statusNum] ?? String(req.status);
          const badgeClass = statusLabel === 'Active' ? 'badge-success' : statusLabel === 'Superseded' ? 'badge-warning' : 'badge-danger';
          return `
            <tr>
              <td style="font-family: var(--font-code); font-weight: 700; color: var(--accent-cyan);">${escapeHtml(req.requirementId)}</td>
              <td style="font-weight: 600;">${escapeHtml(req.title)}</td>
              <td style="color: var(--text-secondary); max-width: 400px;">${escapeHtml(req.body)}</td>
              <td><span class="badge ${badgeClass}">${statusLabel}</span></td>
            </tr>
          `;
        }).join('')}
      </tbody>
    </table>
  `;
}

function escapeHtml(value: string): string {
  const div = document.createElement('div');
  div.textContent = value;
  return div.innerHTML;
}
