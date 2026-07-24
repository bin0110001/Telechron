import { createFirstAdmin, ApiError } from '../api';

// R-SEC6: shown only when GET /api/setup/status reports isSetupComplete
// === false (checked by main.ts before this ever renders) -- creates the
// very first Admin User via the one-time setup-token bootstrap path.
export function renderSetupView(): string {
  return `
    <div style="display: flex; align-items: center; justify-content: center; min-height: 80vh;">
      <div class="card" style="max-width: 420px; width: 100%;">
        <div class="card-title" style="margin-bottom: 4px;">Set up Telechron</div>
        <p style="font-size: 12px; color: var(--text-muted); margin-bottom: 16px;">
          No Users exist yet. Create the first Admin account using the setup token
          your operator configured (TELECHRON_SETUP_TOKEN). See the README for details.
        </p>
        <form id="setup-form">
          <div style="margin-bottom: 12px;">
            <label style="font-size: 12px; color: var(--text-muted);">Setup Token</label>
            <input type="password" id="setup-token" required style="width: 100%; padding: 8px; margin-top: 4px; background: rgba(255,255,255,0.03); border: 1px solid var(--border-color); border-radius: var(--radius-sm); color: var(--text-primary);" />
          </div>
          <div style="margin-bottom: 12px;">
            <label style="font-size: 12px; color: var(--text-muted);">Display Name</label>
            <input type="text" id="setup-display-name" required style="width: 100%; padding: 8px; margin-top: 4px; background: rgba(255,255,255,0.03); border: 1px solid var(--border-color); border-radius: var(--radius-sm); color: var(--text-primary);" />
          </div>
          <div style="margin-bottom: 12px;">
            <label style="font-size: 12px; color: var(--text-muted);">Email</label>
            <input type="email" id="setup-email" required style="width: 100%; padding: 8px; margin-top: 4px; background: rgba(255,255,255,0.03); border: 1px solid var(--border-color); border-radius: var(--radius-sm); color: var(--text-primary);" />
          </div>
          <div style="margin-bottom: 16px;">
            <label style="font-size: 12px; color: var(--text-muted);">Password (12+ characters)</label>
            <input type="password" id="setup-password" required minlength="12" style="width: 100%; padding: 8px; margin-top: 4px; background: rgba(255,255,255,0.03); border: 1px solid var(--border-color); border-radius: var(--radius-sm); color: var(--text-primary);" />
          </div>
          <div id="setup-error" style="color: var(--accent-rose); font-size: 12px; margin-bottom: 12px; display: none;"></div>
          <div id="setup-success" style="color: var(--accent-emerald); font-size: 12px; margin-bottom: 12px; display: none;"></div>
          <button type="submit" class="btn btn-primary" style="width: 100%;">Create Admin Account</button>
        </form>
      </div>
    </div>
  `;
}

export function wireSetupView(onSetupComplete: () => void): void {
  const form = document.getElementById('setup-form') as HTMLFormElement | null;
  const errorEl = document.getElementById('setup-error');
  const successEl = document.getElementById('setup-success');
  if (!form) return;

  form.addEventListener('submit', async (e) => {
    e.preventDefault();
    const setupToken = (document.getElementById('setup-token') as HTMLInputElement).value;
    const displayName = (document.getElementById('setup-display-name') as HTMLInputElement).value;
    const email = (document.getElementById('setup-email') as HTMLInputElement).value;
    const password = (document.getElementById('setup-password') as HTMLInputElement).value;

    if (errorEl) errorEl.style.display = 'none';
    if (successEl) successEl.style.display = 'none';

    try {
      await createFirstAdmin(setupToken, email, password, displayName);
      if (successEl) {
        successEl.textContent = 'Admin account created. Redirecting to login…';
        successEl.style.display = 'block';
      }
      setTimeout(onSetupComplete, 1200);
    } catch (err) {
      if (errorEl) {
        errorEl.textContent = err instanceof ApiError ? err.message : 'Setup failed.';
        errorEl.style.display = 'block';
      }
    }
  });
}
