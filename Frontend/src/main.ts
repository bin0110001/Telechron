import './style.css';
import { renderDashboardView } from './views/DashboardView';
import { renderRunsView } from './views/RunsView';
import { renderProjectsView } from './views/ProjectsView';
import { renderWorkflowsView } from './views/WorkflowsView';
import { renderMachinesView } from './views/MachinesView';
import { renderModulesView } from './views/ModulesView';
import { renderLlmView } from './views/LlmView';
import { renderSchedulingView } from './views/SchedulingView';
import { renderSecretsView } from './views/SecretsView';
import { renderDesignDocView } from './views/DesignDocView';
import { renderApprovalsView } from './views/ApprovalsView';
import { renderPrivilegedDiffView } from './views/PrivilegedDiffView';
import { renderProvenanceView } from './views/ProvenanceView';
import { renderAuditLogView } from './views/AuditLogView';

const viewsMap: Record<string, { title: string; subtitle: string; render: () => string }> = {
  dashboard: { title: 'Dashboard', subtitle: 'Realtime System Status & Overview', render: renderDashboardView },
  runs: { title: 'Runs & Work Queue', subtitle: 'Realtime Workflow Execution Status', render: renderRunsView },
  projects: { title: 'Managed Projects', subtitle: 'Project Repositories & Design Document Integration', render: renderProjectsView },
  workflows: { title: 'Workflow Graph Editor', subtitle: 'Interactive Visual DAG Node Editor', render: renderWorkflowsView },
  machines: { title: 'Machines & Resources', subtitle: 'Host Topology & Exclusive Resource Locks', render: renderMachinesView },
  modules: { title: 'Modules & Connectors', subtitle: 'Hot-Reloadable Module Catalog & Self-Tests', render: renderModulesView },
  llm: { title: 'LLM & Cost Dashboard', subtitle: 'Model Spend Caps & Prompt Isolation Status', render: renderLlmView },
  scheduling: { title: 'Schedule Manager', subtitle: 'Durable Cron & Serialization Rules', render: renderSchedulingView },
  secrets: { title: 'Secrets Management', subtitle: 'Encrypted Vault & Scope Resolution', render: renderSecretsView },
  designdoc: { title: 'Design Document Manager', subtitle: 'Living Requirements & Drift Markers', render: renderDesignDocView },
  approvals: { title: 'Human Approval Queue', subtitle: 'Privileged Gate Approvals with Approver Attribution', render: renderApprovalsView },
  privilegeddiff: { title: 'Privileged Diff Review', subtitle: 'Side-by-Side Patch Review for Privileged Paths', render: renderPrivilegedDiffView },
  provenance: { title: 'Signed Provenance', subtitle: 'Cryptographic Commit Provenance & Attestation', render: renderProvenanceView },
  auditlog: { title: 'Immutable Audit Log', subtitle: 'Trace ID Correlation Audit Logs', render: renderAuditLogView },
};

function navigateTo(viewName: string) {
  const target = viewsMap[viewName] || viewsMap.dashboard;
  
  // Update header
  const titleEl = document.getElementById('page-title');
  const subtitleEl = document.getElementById('page-subtitle');
  if (titleEl) titleEl.textContent = target.title;
  if (subtitleEl) subtitleEl.textContent = target.subtitle;

  // Update navigation items active state
  document.querySelectorAll('.nav-item').forEach(item => {
    item.classList.remove('active');
  });
  const activeNav = document.getElementById(`nav-${viewName}`);
  if (activeNav) activeNav.classList.add('active');

  // Render view
  const container = document.getElementById('view-container');
  if (container) {
    container.innerHTML = target.render();
  }
}

// Router Event Listeners
window.addEventListener('hashchange', () => {
  const hash = window.location.hash.replace('#', '');
  navigateTo(hash);
});

document.addEventListener('DOMContentLoaded', () => {
  const initialHash = window.location.hash.replace('#', '') || 'dashboard';
  navigateTo(initialHash);
});
