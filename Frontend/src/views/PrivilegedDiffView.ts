export function renderPrivilegedDiffView(): string {
  return `
    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px;">
      <div>
        <h2 style="font-family: var(--font-heading); font-size: 18px; font-weight: 700;">Privileged Path Diff Review</h2>
        <p style="font-size: 12px; color: var(--text-muted);">Side-by-side patch diff viewer for privileged path modifications, repair pipeline fixes, and drift findings (R-SEC4/R-FIX12/13).</p>
      </div>
      <button class="btn btn-primary" style="background: var(--accent-emerald);">Approve & Apply Diff</button>
    </div>

    <!-- Side-by-Side Diff Viewer Box -->
    <div class="card" style="font-family: var(--font-code); font-size: 12px; background: #080a0e; overflow-x: auto;">
      <div style="padding: 8px 12px; border-bottom: 1px solid var(--border-color); color: var(--text-muted); font-size: 11px;">
        Diff for: <strong>Host/Reliability/HostSentinel.cs</strong> (Gate: R-SEC4-privileged-path)
      </div>
      <pre style="margin: 0; padding: 16px; line-height: 1.6;">
<span style="color: #6b7280;">@@ -24,8 +24,8 @@</span>
<span style="color: #f43f5e;">- public bool EnableUnrestrictedExecution = true;</span>
<span style="color: #10b981;">+ public bool EnableUnrestrictedExecution = false; // Privileged path enforcement</span>
<span style="color: #6b7280;">  public Task&lt;HostSelfRepairReport&gt; RunSelfRepairCheckAsync()</span>
      </pre>
    </div>
  `;
}
