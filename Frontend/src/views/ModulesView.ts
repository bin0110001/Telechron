import { mockModules } from '../store';

export function renderModulesView(): string {
  return `
    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px;">
      <div>
        <h2 style="font-family: var(--font-heading); font-size: 18px; font-weight: 700;">Modules, Connectors & Toolchains</h2>
        <p style="font-size: 12px; color: var(--text-muted);">Loaded function executors, LLM engines, connectors, and toolchain modules with hot-reload support (R-MOD1-6).</p>
      </div>
      <button class="btn btn-primary">⚡ Hot-Reload All</button>
    </div>

    <div class="table-container">
      <table class="data-table">
        <thead>
          <tr>
            <th>Module Name</th>
            <th>Kind</th>
            <th>Version</th>
            <th>Runtime Status</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          ${mockModules.map(mod => `
            <tr>
              <td style="font-weight: 600;">${mod.name}</td>
              <td><span class="badge badge-primary">${mod.kind}</span></td>
              <td style="font-family: var(--font-code);">${mod.version}</td>
              <td><span class="badge badge-success">${mod.status}</span></td>
              <td>
                <button class="btn btn-primary" style="font-size: 11px; padding: 2px 8px;">Run Self-Test</button>
                <button class="btn btn-primary" style="font-size: 11px; padding: 2px 8px; background: rgba(255,255,255,0.08);">Reload</button>
              </td>
            </tr>
          `).join('')}
        </tbody>
      </table>
    </div>
  `;
}
