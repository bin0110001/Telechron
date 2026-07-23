import { mockProjects } from '../store';

export function renderProjectsView(): string {
  return `
    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px;">
      <div>
        <h2 style="font-family: var(--font-heading); font-size: 18px; font-weight: 700;">Managed Projects</h2>
        <p style="font-size: 12px; color: var(--text-muted);">Registered project codebases, standing design documents, findings, and telemetry (R-DM16/R-UI2).</p>
      </div>
      <button class="btn btn-primary">+ Register Project</button>
    </div>

    <div class="grid-cards">
      ${mockProjects.map(project => `
        <div class="card">
          <div class="card-title">${project.name} <span class="badge badge-primary">Active</span></div>
          <div style="font-family: var(--font-code); font-size: 11px; color: var(--accent-cyan); margin: 8px 0;">${project.repoPath}</div>
          <div style="font-size: 12px; color: var(--text-secondary); margin-bottom: 8px;"><strong>Design Doc:</strong> ${project.activeDesignDoc}</div>
          <div style="display: flex; justify-content: space-between; align-items: center; margin-top: 12px;">
            <span class="badge ${project.activeFindingsCount === 0 ? 'badge-success' : 'badge-warning'}">${project.activeFindingsCount} Active Findings</span>
            <button class="btn btn-primary" style="font-size: 11px; padding: 4px 10px;">Project Details</button>
          </div>
        </div>
      `).join('')}
    </div>
  `;
}
