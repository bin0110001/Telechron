import { login, ApiError } from '../api';

export function renderLoginView(): string {
  // Rendered once; the actual submit handler is wired in wireLoginView
  // after this markup is inserted into the DOM (see main.ts).
  return `
    <div style="display: flex; align-items: center; justify-content: center; min-height: 80vh;">
      <div class="card" style="max-width: 360px; width: 100%;">
        <div class="card-title" style="margin-bottom: 16px;">Sign in to Telechron</div>
        <form id="login-form">
          <div style="margin-bottom: 12px;">
            <label style="font-size: 12px; color: var(--text-muted);">Email</label>
            <input type="email" id="login-email" required style="width: 100%; padding: 8px; margin-top: 4px; background: rgba(255,255,255,0.03); border: 1px solid var(--border-color); border-radius: var(--radius-sm); color: var(--text-primary);" />
          </div>
          <div style="margin-bottom: 16px;">
            <label style="font-size: 12px; color: var(--text-muted);">Password</label>
            <input type="password" id="login-password" required style="width: 100%; padding: 8px; margin-top: 4px; background: rgba(255,255,255,0.03); border: 1px solid var(--border-color); border-radius: var(--radius-sm); color: var(--text-primary);" />
          </div>
          <div id="login-error" style="color: var(--accent-rose); font-size: 12px; margin-bottom: 12px; display: none;"></div>
          <button type="submit" class="btn btn-primary" style="width: 100%;">Sign in</button>
        </form>
      </div>
    </div>
  `;
}

export function wireLoginView(onSuccess: () => void): void {
  const form = document.getElementById('login-form') as HTMLFormElement | null;
  const errorEl = document.getElementById('login-error');
  if (!form) return;

  form.addEventListener('submit', async (e) => {
    e.preventDefault();
    const email = (document.getElementById('login-email') as HTMLInputElement).value;
    const password = (document.getElementById('login-password') as HTMLInputElement).value;

    if (errorEl) errorEl.style.display = 'none';
    try {
      await login(email, password);
      onSuccess();
    } catch (err) {
      if (errorEl) {
        errorEl.textContent = err instanceof ApiError ? err.message : 'Login failed.';
        errorEl.style.display = 'block';
      }
    }
  });
}
