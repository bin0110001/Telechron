import { apiClient, ApiError, type AuditEventResponse } from '../api';

const AUDIT_EVENT_KIND_LABELS: Record<number, string> = {
  0: 'SecretAccessed', 1: 'SecretCreated', 2: 'SecretRotated', 3: 'SecretRevoked', 4: 'ApprovalDecision',
  5: 'ModuleInstalled', 6: 'CapabilityGranted', 7: 'RepairAutoCommitted', 8: 'AuthenticationFailed', 9: 'AuthorizationDenied',
};

export function renderAuditLogView(): string {
  return `
    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px;">
      <div>
        <h2 style="font-family: var(--font-heading); font-size: 18px; font-weight: 700;">Immutable System Audit Log</h2>
        <p style="font-size: 12px; color: var(--text-muted);">Hash-chained security audit trail (R-SEC7). Admin-only surface.</p>
      </div>
      <span class="badge" id="audit-chain-status" style="padding: 6px 12px; font-size: 12px;">Verifying chain…</span>
    </div>
    <div id="auditlog-content" class="table-container">
      <div class="card">Loading audit log…</div>
    </div>
  `;
}

export async function wireAuditLogView(): Promise<void> {
  const content = document.getElementById('auditlog-content');
  const chainStatus = document.getElementById('audit-chain-status');
  if (!content) return;

  try {
    const events = await apiClient.listAuditEvents(0, 100);
    content.innerHTML = events.length === 0 ? '<div class="card">No audit events yet.</div>' : renderTable(events);
  } catch (err) {
    const message = err instanceof ApiError ? err.message : String(err);
    content.innerHTML = `<div class="card" style="color: var(--accent-rose);">Failed to load audit log: ${
      err instanceof ApiError && err.status === 403 ? 'Admin role required to view the audit log.' : message
    }</div>`;
    if (chainStatus) chainStatus.style.display = 'none';
    return;
  }

  if (chainStatus) {
    try {
      const verification = await apiClient.verifyAuditChain();
      chainStatus.textContent = verification.isIntact ? '✓ Chain Intact' : `⚠ Tampered at #${verification.firstTamperedSequence}`;
      chainStatus.className = `badge ${verification.isIntact ? 'badge-success' : 'badge-danger'}`;
    } catch {
      chainStatus.textContent = 'Chain verification failed';
      chainStatus.className = 'badge badge-warning';
    }
  }
}

function renderTable(events: AuditEventResponse[]): string {
  return `
    <table class="data-table">
      <thead>
        <tr>
          <th>Sequence</th>
          <th>Timestamp (UTC)</th>
          <th>Kind</th>
          <th>Actor</th>
          <th>Project</th>
          <th>Detail</th>
        </tr>
      </thead>
      <tbody>
        ${events.map((e) => {
          const kindNum = typeof e.kind === 'number' ? e.kind : Number(e.kind);
          const kindLabel = AUDIT_EVENT_KIND_LABELS[kindNum] ?? String(e.kind);
          return `
            <tr>
              <td style="font-family: var(--font-code); font-size: 12px;">${e.sequence}</td>
              <td style="color: var(--text-muted); font-size: 12px;">${e.occurredAtUtc}</td>
              <td><span class="badge badge-primary">${escapeHtml(kindLabel)}</span></td>
              <td style="font-family: var(--font-code); font-size: 11px;">${e.actorUserId ?? 'system'}</td>
              <td style="font-family: var(--font-code); font-size: 11px;">${e.projectId ?? '—'}</td>
              <td style="font-family: var(--font-code); font-size: 11px; color: var(--text-muted); max-width: 300px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;">${escapeHtml(e.detailJson)}</td>
            </tr>
          `;
        }).join('')}
      </tbody>
    </table>
  `;
}

function escapeHtml(value: string): string {
  const div = document.createElement('div');
  div.textContent = value;
  return div.innerHTML;
}
