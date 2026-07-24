import { apiClient, ApiError, type WorkflowResponse } from '../api';

interface WorkflowStepDefinition {
  id: string;
  name: string;
  functionKind: string;
  dependsOnStepIds?: string[];
  moduleId?: string | null;
}

interface WorkflowDefinition {
  name: string;
  steps: WorkflowStepDefinition[];
}

const NODE_WIDTH = 220;
const NODE_HEIGHT = 70;
const COLUMN_GAP = 120;
const ROW_GAP = 40;

export function renderWorkflowsView(): string {
  return `
    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px;">
      <div>
        <h2 style="font-family: var(--font-heading); font-size: 18px; font-weight: 700;">Workflow Graph</h2>
        <p style="font-size: 12px; color: var(--text-muted);">Real step dependency graph, rendered from the Workflow's own definition (R-WF1/R-WF6).</p>
      </div>
      <select id="workflows-project-select" style="background: rgba(255,255,255,0.03); border: 1px solid var(--border-color); color: var(--text-primary); border-radius: var(--radius-sm); padding: 6px;">
        <option value="">Loading projects…</option>
      </select>
    </div>
    <div id="workflows-content">
      <div class="card">Select a Project to view its Workflows.</div>
    </div>
  `;
}

export async function wireWorkflowsView(): Promise<void> {
  const select = document.getElementById('workflows-project-select') as HTMLSelectElement | null;
  const content = document.getElementById('workflows-content');
  if (!select || !content) return;

  try {
    const projects = await apiClient.listProjects();
    if (projects.length === 0) {
      select.innerHTML = '<option value="">No Projects visible</option>';
      content.innerHTML = '<div class="card">No Projects visible to your account yet.</div>';
      return;
    }

    select.innerHTML = projects.map((p) => `<option value="${p.id}">${escapeHtml(p.name)}</option>`).join('');
    select.addEventListener('change', () => loadWorkflowsForProject(select.value, content));
    await loadWorkflowsForProject(select.value, content);
  } catch (err) {
    select.innerHTML = '<option value="">Error</option>';
    content.innerHTML = `<div class="card" style="color: var(--accent-rose);">Failed to load Projects: ${
      err instanceof ApiError ? err.message : String(err)
    }</div>`;
  }
}

async function loadWorkflowsForProject(projectId: string, content: HTMLElement): Promise<void> {
  content.innerHTML = '<div class="card">Loading workflows…</div>';
  try {
    const workflows = await apiClient.listWorkflows(projectId);
    if (workflows.length === 0) {
      content.innerHTML = '<div class="card">No Workflows defined for this Project yet.</div>';
      return;
    }

    content.innerHTML = `
      <select id="workflow-select" style="background: rgba(255,255,255,0.03); border: 1px solid var(--border-color); color: var(--text-primary); border-radius: var(--radius-sm); padding: 6px; margin-bottom: 16px;">
        ${workflows.map((w) => `<option value="${w.id}">${escapeHtml(w.name)}</option>`).join('')}
      </select>
      <div id="workflow-graph-container"></div>
    `;

    const workflowSelect = document.getElementById('workflow-select') as HTMLSelectElement;
    const graphContainer = document.getElementById('workflow-graph-container')!;
    const renderSelected = () => {
      const selected = workflows.find((w) => w.id === workflowSelect.value);
      graphContainer.innerHTML = selected ? renderWorkflowGraph(selected) : '';
    };
    workflowSelect.addEventListener('change', renderSelected);
    renderSelected();
  } catch (err) {
    content.innerHTML = `<div class="card" style="color: var(--accent-rose);">Failed to load Workflows: ${
      err instanceof ApiError ? err.message : String(err)
    }</div>`;
  }
}

function renderWorkflowGraph(workflow: WorkflowResponse): string {
  let definition: WorkflowDefinition;
  try {
    definition = JSON.parse(workflow.definitionJson);
  } catch {
    return `<div class="card" style="color: var(--accent-rose);">Could not parse this Workflow's definition JSON.</div>`;
  }

  const steps = definition.steps ?? [];
  if (steps.length === 0) {
    return '<div class="card">This Workflow has no steps.</div>';
  }

  const layers = layoutIntoLayers(steps);
  const positions = new Map<string, { x: number; y: number }>();
  layers.forEach((layer, columnIndex) => {
    layer.forEach((step, rowIndex) => {
      positions.set(step.id, {
        x: columnIndex * (NODE_WIDTH + COLUMN_GAP) + 20,
        y: rowIndex * (NODE_HEIGHT + ROW_GAP) + 20,
      });
    });
  });

  const canvasWidth = layers.length * (NODE_WIDTH + COLUMN_GAP) + 40;
  const canvasHeight = Math.max(...layers.map((l) => l.length)) * (NODE_HEIGHT + ROW_GAP) + 40;

  const edges = steps.flatMap((step) =>
    (step.dependsOnStepIds ?? [])
      .filter((depId) => positions.has(depId))
      .map((depId) => {
        const from = positions.get(depId)!;
        const to = positions.get(step.id)!;
        const x1 = from.x + NODE_WIDTH;
        const y1 = from.y + NODE_HEIGHT / 2;
        const x2 = to.x;
        const y2 = to.y + NODE_HEIGHT / 2;
        const midX = (x1 + x2) / 2;
        return `<path d="M ${x1} ${y1} C ${midX} ${y1}, ${midX} ${y2}, ${x2} ${y2}" stroke="var(--accent-primary)" stroke-width="2" fill="none" />`;
      })
  );

  const nodes = steps.map((step) => {
    const pos = positions.get(step.id)!;
    return `
      <div class="graph-node" style="position: absolute; top: ${pos.y}px; left: ${pos.x}px; width: ${NODE_WIDTH}px;">
        <div class="graph-node-title">${escapeHtml(step.name)}</div>
        <div class="graph-node-type">${escapeHtml(step.functionKind)}</div>
      </div>
    `;
  });

  return `
    <div class="graph-canvas" style="position: relative; width: ${canvasWidth}px; height: ${canvasHeight}px; overflow: auto;">
      <svg style="position: absolute; width: 100%; height: 100%; pointer-events: none;">
        ${edges.join('')}
      </svg>
      ${nodes.join('')}
    </div>
  `;
}

// Real topological layering by DependsOnStepIds -- a step's column is one
// past the maximum column of anything it depends on, so the graph reads
// left-to-right in real dependency order rather than a fixed illustration.
function layoutIntoLayers(steps: WorkflowStepDefinition[]): WorkflowStepDefinition[][] {
  const stepById = new Map(steps.map((s) => [s.id, s]));
  const columnOf = new Map<string, number>();

  function resolveColumn(stepId: string, visiting: Set<string>): number {
    if (columnOf.has(stepId)) return columnOf.get(stepId)!;
    if (visiting.has(stepId)) return 0; // cycle guard -- treat as no further dependency

    visiting.add(stepId);
    const step = stepById.get(stepId);
    const deps = step?.dependsOnStepIds ?? [];
    const column = deps.length === 0
      ? 0
      : Math.max(...deps.filter((d) => stepById.has(d)).map((d) => resolveColumn(d, visiting))) + 1;
    visiting.delete(stepId);
    columnOf.set(stepId, column);
    return column;
  }

  steps.forEach((s) => resolveColumn(s.id, new Set()));

  const maxColumn = Math.max(...Array.from(columnOf.values()));
  const layers: WorkflowStepDefinition[][] = Array.from({ length: maxColumn + 1 }, () => []);
  steps.forEach((s) => layers[columnOf.get(s.id) ?? 0].push(s));
  return layers;
}

function escapeHtml(value: string): string {
  const div = document.createElement('div');
  div.textContent = value;
  return div.innerHTML;
}
