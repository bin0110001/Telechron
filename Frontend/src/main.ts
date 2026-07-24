import './style.css';
import { isAuthenticated, logout, getSetupStatus, ApiError } from './api';
import { renderLoginView, wireLoginView } from './views/LoginView';
import { renderSetupView, wireSetupView } from './views/SetupView';
import { renderDashboardView, wireDashboardView } from './views/DashboardView';
import { renderRunsView, wireRunsView } from './views/RunsView';
import { renderProjectsView, wireProjectsView } from './views/ProjectsView';
import { renderWorkflowsView, wireWorkflowsView } from './views/WorkflowsView';
import { renderMachinesView, wireMachinesView } from './views/MachinesView';
import { renderModulesView, wireModulesView } from './views/ModulesView';
import { renderLlmView, wireLlmView } from './views/LlmView';
import { renderSchedulingView, wireSchedulingView } from './views/SchedulingView';
import { renderSecretsView, wireSecretsView } from './views/SecretsView';
import { renderDesignDocView, wireDesignDocView } from './views/DesignDocView';
import { renderApprovalsView, wireApprovalsView } from './views/ApprovalsView';
import { renderPrivilegedDiffView, wirePrivilegedDiffView } from './views/PrivilegedDiffView';
import { renderProvenanceView, wireProvenanceView } from './views/ProvenanceView';
import { renderAuditLogView, wireAuditLogView } from './views/AuditLogView';

interface ViewEntry {
  title: string;
  subtitle: string;
  render: () => string;
  // Called after render()'s markup is in the DOM -- fetches real data and
  // patches the DOM, and (for surfaces where staleness genuinely matters --
  // Runs/Approvals/DesignDocument) sets up a polling refresh interval.
  // Returns a cleanup function to clear that interval when the user
  // navigates away, so surfaces don't keep polling in the background
  // forever.
  wire?: () => (() => void) | void;
}

const viewsMap: Record<string, ViewEntry> = {
  dashboard: { title: 'Dashboard', subtitle: 'Real System Overview', render: renderDashboardView, wire: () => pollingWire(wireDashboardView) },
  runs: { title: 'Workflow Runs', subtitle: 'Live Run Status Per Project', render: renderRunsView, wire: () => pollingWire(wireRunsView) },
  projects: { title: 'Managed Projects', subtitle: 'Project Repositories & Design Document Integration', render: renderProjectsView, wire: () => pollingWire(() => wireProjectsView(navigateToProjectRuns)) },
  workflows: { title: 'Workflow Graph', subtitle: 'Real Step Dependency Graph (R-WF1/R-WF6)', render: renderWorkflowsView, wire: () => { void wireWorkflowsView(); } },
  machines: { title: 'Machines', subtitle: 'Registered Agent-Hosting Machines', render: renderMachinesView, wire: () => pollingWire(wireMachinesView) },
  modules: { title: 'Modules', subtitle: 'Installed Modules & Declared Capabilities', render: renderModulesView, wire: () => { void wireModulesView(); } },
  llm: { title: 'LLM & Cost Dashboard', subtitle: 'Connections & Per-Project Spend (R-LLM3/4)', render: renderLlmView, wire: () => { void wireLlmView(); } },
  scheduling: { title: 'Schedule Manager', subtitle: 'Durable Cron & Serialization Rules', render: renderSchedulingView, wire: () => { void wireSchedulingView(); } },
  secrets: { title: 'Secrets Management', subtitle: 'Handle-Only View (R-SEC1)', render: renderSecretsView, wire: () => { void wireSecretsView(); } },
  designdoc: { title: 'Design Document Manager', subtitle: 'Living Requirements (R-DM16)', render: renderDesignDocView, wire: () => pollingWire(wireDesignDocView) },
  approvals: { title: 'Human Approval Queue', subtitle: 'Live Pending Gate Approvals (R-WF5/DM15)', render: renderApprovalsView, wire: () => pollingWire(wireApprovalsView) },
  privilegeddiff: { title: 'Privileged Diff Review', subtitle: 'Pending Repair Patches (R-SEC4/R-FIX12/13)', render: renderPrivilegedDiffView, wire: () => pollingWire(wirePrivilegedDiffView) },
  provenance: { title: 'Repair Provenance', subtitle: 'Signed Attestation Records (R-SEC3/R-DM3a)', render: renderProvenanceView, wire: () => { void wireProvenanceView(); } },
  auditlog: { title: 'Immutable Audit Log', subtitle: 'Hash-Chained Security Trail (R-SEC7)', render: renderAuditLogView, wire: () => { void wireAuditLogView(); } },
};

const POLL_INTERVAL_MS = 10_000;
let activePollHandle: ReturnType<typeof setInterval> | null = null;

// Wraps a view's async wire() function so its initial fetch runs
// immediately and then again on a fixed interval, giving surfaces where
// staleness matters most (Dashboard/Runs/Approvals/DesignDocument/
// PrivilegedDiff) a basic realtime refresh (R-UI3) without needing a
// WebSocket/SSE transport the backend doesn't have yet.
function pollingWire(wireFn: () => Promise<void>): () => void {
  void wireFn();
  const handle = setInterval(() => void wireFn(), POLL_INTERVAL_MS);
  return () => clearInterval(handle);
}

function navigateToProjectRuns(projectId: string): void {
  window.location.hash = 'runs';
  // RunsView's own project <select> handles the actual scoping; a full
  // deep-link (e.g. #runs/<projectId>) is a reasonable follow-up once
  // more than one child route exists.
  console.info(`Navigated to Runs for Project ${projectId}`);
}

function navigateTo(viewName: string) {
  if (activePollHandle) {
    clearInterval(activePollHandle);
    activePollHandle = null;
  }

  const target = viewsMap[viewName] || viewsMap.dashboard;

  const titleEl = document.getElementById('page-title');
  const subtitleEl = document.getElementById('page-subtitle');
  if (titleEl) titleEl.textContent = target.title;
  if (subtitleEl) subtitleEl.textContent = target.subtitle;

  document.querySelectorAll('.nav-item').forEach((item) => item.classList.remove('active'));
  document.getElementById(`nav-${viewName}`)?.classList.add('active');

  const container = document.getElementById('view-container');
  if (container) {
    container.innerHTML = target.render();
  }

  if (target.wire) {
    const cleanup = target.wire();
    if (cleanup) activePollHandle = cleanup as unknown as ReturnType<typeof setInterval>;
  }
}

async function renderApp(): Promise<void> {
  const app = document.getElementById('app');
  if (!app) return;

  if (!isAuthenticated()) {
    const viewContainer = document.getElementById('view-container');
    const sidebar = document.querySelector('.sidebar') as HTMLElement | null;
    const header = document.querySelector('.top-header') as HTMLElement | null;
    if (sidebar) sidebar.style.display = 'none';
    if (header) header.style.display = 'none';
    if (!viewContainer) return;

    // R-SEC6: before showing the ordinary login form, check whether any
    // User exists at all -- a fresh Host has none, and there is no
    // self-registration path except the one-time setup-token bootstrap.
    let setupComplete = true;
    try {
      setupComplete = (await getSetupStatus()).isSetupComplete;
    } catch (err) {
      // Host unreachable or setup status endpoint itself failing --
      // fall through to the ordinary login form, which will surface the
      // same underlying error on submit rather than getting stuck here.
      console.error('Failed to check setup status:', err instanceof ApiError ? err.message : err);
    }

    if (!setupComplete) {
      viewContainer.innerHTML = renderSetupView();
      wireSetupView(() => void renderApp());
    } else {
      viewContainer.innerHTML = renderLoginView();
      wireLoginView(() => void renderApp());
    }
    return;
  }

  const sidebar = document.querySelector('.sidebar') as HTMLElement | null;
  const header = document.querySelector('.top-header') as HTMLElement | null;
  if (sidebar) sidebar.style.display = '';
  if (header) header.style.display = '';

  const initialHash = window.location.hash.replace('#', '') || 'dashboard';
  navigateTo(initialHash);
}

window.addEventListener('hashchange', () => {
  if (!isAuthenticated()) return;
  const hash = window.location.hash.replace('#', '');
  navigateTo(hash);
});

document.addEventListener('DOMContentLoaded', () => {
  void renderApp();

  const logoutBtn = document.getElementById('nav-logout');
  logoutBtn?.addEventListener('click', () => {
    logout();
    void renderApp();
  });
});
