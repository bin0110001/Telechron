import { apiClient, ApiError, type ModuleResponse } from '../api';

export function renderModulesView(): string {
  return `
    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px;">
      <div>
        <h2 style="font-family: var(--font-heading); font-size: 18px; font-weight: 700;">Modules</h2>
        <p style="font-size: 12px; color: var(--text-muted);">Installed function executors, LLM engines, connectors, and toolchain modules (R-MOD1-6).</p>
      </div>
    </div>
    <div id="modules-content" class="table-container">
      <div class="card">Loading modules…</div>
    </div>
  `;
}

export async function wireModulesView(): Promise<void> {
  const content = document.getElementById('modules-content');
  if (!content) return;

  try {
    const modules = await apiClient.listModules();
    content.innerHTML = modules.length === 0 ? '<div class="card">No Modules installed yet.</div>' : renderTable(modules);
  } catch (err) {
    content.innerHTML = `<div class="card" style="color: var(--accent-rose);">Failed to load Modules: ${
      err instanceof ApiError ? err.message : String(err)
    }</div>`;
  }
}

function renderTable(modules: ModuleResponse[]): string {
  return `
    <table class="data-table">
      <thead>
        <tr>
          <th>Module Name</th>
          <th>Kind</th>
          <th>Version</th>
          <th>Declared Capabilities</th>
          <th>Installed</th>
        </tr>
      </thead>
      <tbody>
        ${modules.map((mod) => `
          <tr>
            <td style="font-weight: 600;">${escapeHtml(mod.name)}</td>
            <td><span class="badge badge-primary">${escapeHtml(mod.kind)}</span></td>
            <td style="font-family: var(--font-code);">${escapeHtml(mod.version)}</td>
            <td style="font-size: 11px; color: var(--text-muted);">${mod.capabilities.length === 0 ? '—' : mod.capabilities.map(escapeHtml).join(', ')}</td>
            <td style="color: var(--text-muted);">${mod.installedAtUtc}</td>
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
