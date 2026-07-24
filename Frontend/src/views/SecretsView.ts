import { apiClient, ApiError, type SecretResponse } from '../api';

export function renderSecretsView(): string {
  return `
    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px;">
      <div>
        <h2 style="font-family: var(--font-heading); font-size: 18px; font-weight: 700;">Secrets Management</h2>
        <p style="font-size: 12px; color: var(--text-muted);">Handle-only view — raw values are never sent to this UI (R-SEC1/R-SEC5).</p>
      </div>
      <select id="secrets-project-select" style="background: rgba(255,255,255,0.03); border: 1px solid var(--border-color); color: var(--text-primary); border-radius: var(--radius-sm); padding: 6px;">
        <option value="">Loading projects…</option>
      </select>
    </div>
    <div id="secrets-content" class="table-container">
      <div class="card">Select a Project to view its Secrets.</div>
    </div>
  `;
}

export async function wireSecretsView(): Promise<void> {
  const select = document.getElementById('secrets-project-select') as HTMLSelectElement | null;
  const content = document.getElementById('secrets-content');
  if (!select || !content) return;

  try {
    const projects = await apiClient.listProjects();
    if (projects.length === 0) {
      select.innerHTML = '<option value="">No Projects visible</option>';
      content.innerHTML = '<div class="card">No Projects visible to your account yet.</div>';
      return;
    }

    select.innerHTML = projects.map((p) => `<option value="${p.id}">${escapeHtml(p.name)}</option>`).join('');
    select.addEventListener('change', () => loadSecretsForProject(select.value, content));
    await loadSecretsForProject(select.value, content);
  } catch (err) {
    select.innerHTML = '<option value="">Error</option>';
    content.innerHTML = `<div class="card" style="color: var(--accent-rose);">Failed to load Projects: ${
      err instanceof ApiError ? err.message : String(err)
    }</div>`;
  }
}

async function loadSecretsForProject(projectId: string, content: HTMLElement): Promise<void> {
  content.innerHTML = '<div class="card">Loading secrets…</div>';
  try {
    const secrets = await apiClient.listSecrets(projectId);
    content.innerHTML = secrets.length === 0 ? '<div class="card">No Secrets stored for this Project.</div>' : renderTable(secrets);
  } catch (err) {
    content.innerHTML = `<div class="card" style="color: var(--accent-rose);">Failed to load Secrets: ${
      err instanceof ApiError ? err.message : String(err)
    }</div>`;
  }
}

function renderTable(secrets: SecretResponse[]): string {
  return `
    <table class="data-table">
      <thead>
        <tr>
          <th>Name</th>
          <th>Handle</th>
          <th>Created</th>
          <th>Status</th>
        </tr>
      </thead>
      <tbody>
        ${secrets.map((s) => `
          <tr>
            <td style="font-weight: 600;">${escapeHtml(s.name)}</td>
            <td style="font-family: var(--font-code); font-size: 12px; color: var(--accent-cyan);">${escapeHtml(s.handle)}</td>
            <td style="color: var(--text-muted);">${s.createdAtUtc}</td>
            <td><span class="badge ${s.revokedAtUtc ? 'badge-danger' : 'badge-success'}">${s.revokedAtUtc ? 'Revoked' : 'Active'}</span></td>
          </tr>
        `).join('')}
      </tbody>
    </table>
  `;
}

function escapeHtml(value: string): string {
  const div = document.createElement('div');
  div.textContent = value;
  return div.innerHTML;
}
