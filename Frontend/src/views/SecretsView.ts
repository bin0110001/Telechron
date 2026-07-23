export function renderSecretsView(): string {
  return `
    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px;">
      <div>
        <h2 style="font-family: var(--font-heading); font-size: 18px; font-weight: 700;">Secrets Management & Scope Resolution</h2>
        <p style="font-size: 12px; color: var(--text-muted);">Encrypted secret storage, project/persona resolution scopes, and access policy rules (R-SEC5/6).</p>
      </div>
      <button class="btn btn-primary">+ Add Secret Key</button>
    </div>

    <div class="table-container">
      <table class="data-table">
        <thead>
          <tr>
            <th>Secret Key</th>
            <th>Resolution Scope</th>
            <th>Encryption Standard</th>
            <th>Last Updated</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr>
            <td style="font-weight: 600; font-family: var(--font-code);">GITHUB_PAT_TOKEN</td>
            <td><span class="badge badge-primary">Project Scoped</span></td>
            <td>AES-256 GCM (Encrypted)</td>
            <td style="color: var(--text-muted);">Yesterday</td>
            <td><button class="btn btn-primary" style="font-size: 11px; padding: 2px 8px;">Rotate Key</button></td>
          </tr>
        </tbody>
      </table>
    </div>
  `;
}
