export function renderWorkflowsView(): string {
  return `
    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px;">
      <div>
        <h2 style="font-family: var(--font-heading); font-size: 18px; font-weight: 700;">Workflow Graph Editor & DAG Canvas</h2>
        <p style="font-size: 12px; color: var(--text-muted);">Visual DAG graph builder, step dependency graph, and step parameter inspector (R-WF1/R-UI2).</p>
      </div>
      <div style="display: flex; gap: 8px;">
        <button class="btn btn-primary">+ Add Step Node</button>
        <button class="btn btn-primary" style="background: linear-gradient(135deg, var(--accent-emerald), var(--accent-cyan));">⚡ Execute Graph</button>
      </div>
    </div>

    <!-- Graph Canvas Container -->
    <div class="graph-canvas" id="workflow-graph-canvas">
      <!-- Step Node 1: Checkout Git Repo -->
      <div class="graph-node" style="top: 140px; left: 80px; border-color: var(--accent-cyan);">
        <div class="graph-node-title">📁 Git Checkout Step</div>
        <div class="graph-node-type">Module: GitHubConnector</div>
        <div style="margin-top: 8px;"><span class="badge badge-success">Passed</span></div>
      </div>

      <!-- Step Connector Arrow 1 -->
      <svg style="position: absolute; width: 100%; height: 100%; pointer-events: none;">
        <path d="M 260 180 C 330 180, 330 180, 400 180" stroke="var(--accent-primary)" stroke-width="2" fill="none" stroke-dasharray="4 4" />
        <path d="M 580 180 C 650 180, 650 280, 720 280" stroke="var(--accent-amber)" stroke-width="2" fill="none" />
      </svg>

      <!-- Step Node 2: Run Containerized Build & Test -->
      <div class="graph-node" style="top: 140px; left: 400px; border-color: var(--accent-primary);">
        <div class="graph-node-title">🧪 Container Test Step</div>
        <div class="graph-node-type">Module: DotnetTestRunner</div>
        <div style="margin-top: 8px;"><span class="badge badge-success">Passed (14s)</span></div>
      </div>

      <!-- Step Node 3: Privileged Approval Gate -->
      <div class="graph-node" style="top: 240px; left: 720px; border-color: var(--accent-amber);">
        <div class="graph-node-title">🛡️ Approval Gate</div>
        <div class="graph-node-type">Gate: R-SEC4-privileged</div>
        <div style="margin-top: 8px;"><span class="badge badge-warning">Awaiting Approval</span></div>
      </div>
    </div>

    <!-- Selected Node Details Panel -->
    <div class="card" style="margin-top: 20px;">
      <div class="card-title">SELECTED STEP PARAMETERS & ARTIFACT OUTPUTS</div>
      <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 16px; margin-top: 12px;">
        <div>
          <label style="font-size: 11px; color: var(--text-muted); display: block; margin-bottom: 4px;">Step Name</label>
          <input type="text" value="Container Test Step" class="prompt-input" style="background: rgba(255,255,255,0.05); padding: 8px; border-radius: 4px; width: 100%; border: 1px solid var(--border-color);" />
        </div>
        <div>
          <label style="font-size: 11px; color: var(--text-muted); display: block; margin-bottom: 4px;">Target Module</label>
          <input type="text" value="DotnetTestRunnerModule v1.0.0" class="prompt-input" style="background: rgba(255,255,255,0.05); padding: 8px; border-radius: 4px; width: 100%; border: 1px solid var(--border-color);" />
        </div>
      </div>
    </div>
  `;
}
