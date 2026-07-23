export function renderLlmView(): string {
  return `
    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px;">
      <div>
        <h2 style="font-family: var(--font-heading); font-size: 18px; font-weight: 700;">LLM Configurations & Cost Dashboard</h2>
        <p style="font-size: 12px; color: var(--text-muted);">Model spend limits, rolling window cost tracking, prompt isolation verification status (R-LLM3/4/5).</p>
      </div>
      <button class="btn btn-primary">Configure Spend Limits</button>
    </div>

    <div class="grid-cards">
      <div class="card">
        <div class="card-title">OLLAMA GEMMA4:LATEST <span class="badge badge-success">Local / Zero Cost</span></div>
        <div class="card-metric">$0.00 / mo</div>
        <div style="font-size: 12px; color: var(--accent-emerald); margin-top: 6px;">Prompt Isolation Status: Verified (R-LLM5)</div>
      </div>
      <div class="card">
        <div class="card-title">CLAUDE-3-5-SONNET <span class="badge badge-primary">API Provider</span></div>
        <div class="card-metric">$14.20 / $50.00</div>
        <div style="font-size: 12px; color: var(--text-muted); margin-top: 6px;">Rolling Window Cap: $50.00 / 24h</div>
      </div>
    </div>
  `;
}
