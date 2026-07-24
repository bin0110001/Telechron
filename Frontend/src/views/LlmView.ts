import { apiClient, ApiError, type LlmConnectionResponse, type LlmCallResponse } from '../api';

export function renderLlmView(): string {
  return `
    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px;">
      <div>
        <h2 style="font-family: var(--font-heading); font-size: 18px; font-weight: 700;">LLM Configurations & Cost Dashboard</h2>
        <p style="font-size: 12px; color: var(--text-muted);">Registered connections and per-Project call/spend history (R-LLM3/4).</p>
      </div>
      <select id="llm-project-select" style="background: rgba(255,255,255,0.03); border: 1px solid var(--border-color); color: var(--text-primary); border-radius: var(--radius-sm); padding: 6px;">
        <option value="">Loading projects…</option>
      </select>
    </div>

    <div id="llm-connections-content" class="grid-cards" style="margin-bottom: 24px;">
      <div class="card">Loading connections…</div>
    </div>

    <div id="llm-calls-content" class="table-container">
      <div class="card">Select a Project to view call history.</div>
    </div>
  `;
}

export async function wireLlmView(): Promise<void> {
  const connectionsContent = document.getElementById('llm-connections-content');
  const select = document.getElementById('llm-project-select') as HTMLSelectElement | null;
  const callsContent = document.getElementById('llm-calls-content');
  if (!connectionsContent || !select || !callsContent) return;

  try {
    const connections = await apiClient.listLlmConnections();
    connectionsContent.innerHTML = connections.length === 0
      ? '<div class="card">No LLM Connections configured yet.</div>'
      : connections.map(renderConnectionCard).join('');
  } catch (err) {
    connectionsContent.innerHTML = `<div class="card" style="color: var(--accent-rose);">Failed to load connections: ${
      err instanceof ApiError ? err.message : String(err)
    }</div>`;
  }

  try {
    const projects = await apiClient.listProjects();
    if (projects.length === 0) {
      select.innerHTML = '<option value="">No Projects visible</option>';
      callsContent.innerHTML = '<div class="card">No Projects visible to your account yet.</div>';
      return;
    }

    select.innerHTML = projects.map((p) => `<option value="${p.id}">${escapeHtml(p.name)}</option>`).join('');
    select.addEventListener('change', () => loadCallsForProject(select.value, callsContent));
    await loadCallsForProject(select.value, callsContent);
  } catch (err) {
    select.innerHTML = '<option value="">Error</option>';
    callsContent.innerHTML = `<div class="card" style="color: var(--accent-rose);">Failed to load Projects: ${
      err instanceof ApiError ? err.message : String(err)
    }</div>`;
  }
}

async function loadCallsForProject(projectId: string, content: HTMLElement): Promise<void> {
  content.innerHTML = '<div class="card">Loading call history…</div>';
  try {
    const calls = await apiClient.listLlmCalls(projectId);
    content.innerHTML = calls.length === 0 ? '<div class="card">No LLM calls recorded for this Project in the last 30 days.</div>' : renderCallsTable(calls);
  } catch (err) {
    content.innerHTML = `<div class="card" style="color: var(--accent-rose);">Failed to load call history: ${
      err instanceof ApiError ? err.message : String(err)
    }</div>`;
  }
}

function renderConnectionCard(c: LlmConnectionResponse): string {
  return `
    <div class="card">
      <div class="card-title">${escapeHtml(c.name)} <span class="badge badge-primary">${escapeHtml(c.provider)}</span></div>
      <div style="font-size: 12px; color: var(--text-muted); margin-top: 6px;">${c.secretHandle ? 'Uses a stored Secret handle' : 'No Secret required (e.g. local provider)'}</div>
    </div>
  `;
}

function renderCallsTable(calls: LlmCallResponse[]): string {
  const totalCost = calls.reduce((sum, c) => sum + c.estimatedCostUsd, 0);
  return `
    <div style="padding: 12px 16px; font-size: 13px; color: var(--text-secondary); border-bottom: 1px solid var(--border-color);">
      Total estimated spend (last 30 days): <strong>$${totalCost.toFixed(4)}</strong>
    </div>
    <table class="data-table">
      <thead>
        <tr>
          <th>Model</th>
          <th>Provider</th>
          <th>Tokens (P/C)</th>
          <th>Cost</th>
          <th>Result</th>
          <th>Occurred</th>
        </tr>
      </thead>
      <tbody>
        ${calls.map((c) => `
          <tr>
            <td style="font-weight: 600;">${escapeHtml(c.model)}</td>
            <td>${escapeHtml(c.provider)}</td>
            <td style="font-family: var(--font-code);">${c.promptTokens} / ${c.completionTokens}</td>
            <td style="font-family: var(--font-code);">$${c.estimatedCostUsd.toFixed(4)}</td>
            <td><span class="badge ${c.succeeded ? 'badge-success' : 'badge-danger'}">${c.succeeded ? 'Succeeded' : 'Failed'}</span></td>
            <td style="color: var(--text-muted);">${c.occurredAtUtc}</td>
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
