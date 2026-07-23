export function renderProvenanceView(): string {
  return `
    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px;">
      <div>
        <h2 style="font-family: var(--font-heading); font-size: 18px; font-weight: 700;">Signed Commit Provenance & Attestation</h2>
        <p style="font-size: 12px; color: var(--text-muted);">Signed cryptographic provenance records ("Why did this change?", R-SEC3/R-DM3a).</p>
      </div>
      <span class="badge badge-success" style="padding: 6px 12px; font-size: 12px;">ECDSA P-256 Validated</span>
    </div>

    <div class="card">
      <div style="font-family: var(--font-heading); font-size: 16px; font-weight: 700; margin-bottom: 12px;">Commit: 86d1437 — Phase 8: Workflows, Functions & Intent Planning</div>
      <div style="font-size: 13px; color: var(--text-secondary); margin-bottom: 12px;">
        <strong>Attested By:</strong> Telechron Provenance Signer (ECDSA P-256 Public Key <span style="font-family: var(--font-code); color: var(--accent-cyan);">04a39b...8f</span>)
      </div>
      <div style="font-size: 12px; background: rgba(255,255,255,0.03); padding: 12px; border-radius: 6px; font-family: var(--font-code);">
        {<br>
        &nbsp;&nbsp;"commitHash": "86d1437a85bc920f...",<br>
        &nbsp;&nbsp;"workflowRunId": "run_8f9a2b1c",<br>
        &nbsp;&nbsp;"findingId": "find_9f8a37b4",<br>
        &nbsp;&nbsp;"repairAttemptId": "att_1a2b3c",<br>
        &nbsp;&nbsp;"signature": "MEUCIQDx9f...28a1"<br>
        }
      </div>
    </div>
  `;
}
