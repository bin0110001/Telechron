import { mockRuns, mockMachines, mockApprovals } from '../store';

export function renderDashboardView(): string {
  return `
    <!-- Natural Language Assistant Prompt Bar -->
    <div class="prompt-bar">
      <span style="font-size: 20px;">🤖</span>
      <input type="text" class="prompt-input" placeholder="Ask Telechron Assistant to build a workflow, analyze logs, or plan intent (e.g. 'Add Ollama embedding endpoint')..." id="assistant-prompt-input" />
      <button class="btn btn-primary" id="btn-generate-intent">⚡ Plan Intent</button>
    </div>

    <!-- Overview Metrics Grid -->
    <div class="grid-cards">
      <div class="card">
        <div class="card-title">ACTIVE WORKFLOW RUNS <span class="badge badge-primary">Realtime</span></div>
        <div class="card-metric">${mockRuns.filter(r => r.status === 'Running' || r.status === 'AwaitingApproval').length} Active</div>
        <div style="font-size: 12px; color: var(--text-muted); margin-top: 6px;">Total Solution Runs: ${mockRuns.length}</div>
      </div>
      <div class="card">
        <div class="card-title">PENDING APPROVAL GATES <span class="badge badge-warning">Action Required</span></div>
        <div class="card-metric" style="color: var(--accent-amber);">${mockApprovals.length} Gates</div>
        <div style="font-size: 12px; color: var(--text-muted); margin-top: 6px;">Privileged paths & requirement revisions</div>
      </div>
      <div class="card">
        <div class="card-title">HOST SENTINEL STATUS <span class="badge badge-success">Active</span></div>
        <div class="card-metric" style="color: var(--accent-emerald);">Healthy</div>
        <div style="font-size: 12px; color: var(--text-muted); margin-top: 6px;">Self-Repair & Drift Detection Enabled</div>
      </div>
      <div class="card">
        <div class="card-title">ONLINE MACHINES <span class="badge badge-primary">Cluster</span></div>
        <div class="card-metric">${mockMachines.filter(m => m.status === 'Online').length} / ${mockMachines.length}</div>
        <div style="font-size: 12px; color: var(--text-muted); margin-top: 6px;">Exclusive GPU Locks: Active</div>
      </div>
    </div>

    <!-- Active Runs Table -->
    <div class="table-container" style="margin-top: 24px;">
      <div style="padding: 16px 18px; font-family: var(--font-heading); font-weight: 700; font-size: 15px; border-bottom: 1px solid var(--border-color); display: flex; justify-content: space-between; align-items: center;">
        <span>RECENT WORKFLOW EXECUTION RUNS</span>
        <button class="btn btn-primary" style="font-size: 11px; padding: 4px 10px;">+ New Run</button>
      </div>
      <table class="data-table">
        <thead>
          <tr>
            <th>Run ID</th>
            <th>Workflow</th>
            <th>Project</th>
            <th>Status</th>
            <th>Started</th>
            <th>Duration</th>
          </tr>
        </thead>
        <tbody>
          ${mockRuns.map(run => `
            <tr>
              <td style="font-family: var(--font-code); font-size: 12px; color: var(--accent-cyan);">${run.id}</td>
              <td style="font-weight: 600;">${run.workflowName}</td>
              <td>${run.projectName}</td>
              <td>
                <span class="badge ${run.status === 'Passed' ? 'badge-success' : run.status === 'AwaitingApproval' ? 'badge-warning' : 'badge-danger'}">
                  ${run.status}
                </span>
              </td>
              <td style="color: var(--text-muted);">${run.startedAt}</td>
              <td style="font-family: var(--font-code);">${run.duration}</td>
            </tr>
          `).join('')}
        </tbody>
      </table>
    </div>
  `;
}
