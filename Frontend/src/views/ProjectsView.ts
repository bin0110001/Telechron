import { apiClient, ApiError, type ProjectResponse } from '../api';

// Async views render a loading placeholder synchronously, then fetch and
// patch the DOM once data arrives -- render() itself can't be async since
// main.ts's router calls it synchronously to get initial innerHTML.
export function renderProjectsView(): string {
  return `
    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px;">
      <div>
        <h2 style="font-family: var(--font-heading); font-size: 18px; font-weight: 700;">Managed Projects</h2>
        <p style="font-size: 12px; color: var(--text-muted);">Registered project codebases, standing design documents, and repair policy (R-DM16/R-UI2).</p>
      </div>
    </div>
    <div id="projects-content" class="grid-cards">
      <div class="card">Loading projects…</div>
    </div>
  `;
}

export async function wireProjectsView(navigateToProject: (projectId: string) => void): Promise<void> {
  const container = document.getElementById('projects-content');
  if (!container) return;

  try {
    const projects = await apiClient.listProjects();
    container.innerHTML = projects.length === 0
      ? '<div class="card">No Projects visible to your account yet.</div>'
      : projects.map(renderProjectCard).join('');

    projects.forEach((project) => {
      const btn = document.getElementById(`project-details-${project.id}`);
      btn?.addEventListener('click', () => navigateToProject(project.id));
    });
  } catch (err) {
    container.innerHTML = `<div class="card" style="color: var(--accent-rose);">Failed to load Projects: ${
      err instanceof ApiError ? err.message : String(err)
    }</div>`;
  }
}

function renderProjectCard(project: ProjectResponse): string {
  const policyLabel = project.repairPolicy === 1 || project.repairPolicy === 'FullyAutonomous'
    ? 'Fully Autonomous'
    : 'Require Approval';

  return `
    <div class="card">
      <div class="card-title">${escapeHtml(project.name)} <span class="badge badge-primary">${policyLabel}</span></div>
      <div style="font-family: var(--font-code); font-size: 11px; color: var(--accent-cyan); margin: 8px 0;">${escapeHtml(project.rootPath)}</div>
      <div style="font-size: 12px; color: var(--text-secondary); margin-bottom: 8px;">
        <strong>Toolchain:</strong> ${project.toolchainId ? 'Assigned' : 'Not assigned'} &nbsp;
        <strong>LLM:</strong> ${project.llmConnectionId ? 'Assigned' : 'Not assigned'}
      </div>
      <div style="display: flex; justify-content: flex-end; margin-top: 12px;">
        <button class="btn btn-primary" id="project-details-${project.id}" style="font-size: 11px; padding: 4px 10px;">View Runs</button>
      </div>
    </div>
  `;
}

function escapeHtml(value: string): string {
  const div = document.createElement('div');
  div.textContent = value;
  return div.innerHTML;
}
