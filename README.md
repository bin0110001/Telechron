# Telechron

Telechron is a multi-agent coding/repair/synthesis platform: a Host (ASP.NET Core, REST + gRPC), one or more Agents (execute work on managed Machines over mTLS gRPC), and a Vite/TypeScript Frontend.

This guide gets a fresh checkout running locally and walks through creating your first (Admin) user.

## Prerequisites

- .NET 10 SDK
- Node.js (for the Frontend)
- PowerShell (for the dev-cert generation script; the launcher scripts are Windows `.bat` files)

## Quick start (Windows)

From the repo root:

```bat
start-all.bat
```

This will, on first run:

1. Generate local mTLS dev certificates into `certs\` (via `scripts\Generate-DevCerts.ps1`) if they don't already exist.
2. Install Frontend dependencies (`npm install`) if `Frontend\node_modules` is missing.
3. Launch three processes, each in its own window:
   - **Host (backend API)** — REST on `http://localhost:5280`, gRPC on `https://localhost:5300`
   - **Agent** — connects to the Host's gRPC endpoint over mTLS
   - **Frontend (Vite dev server)** — `http://localhost:5173`

`start-all.bat` sets a fixed set of **development-only** secrets (JWT signing key, mTLS cert passwords, agent enrollment token) as environment variables for the child processes. These are fine for local development but must never be reused anywhere real — see [Configuration](#configuration) below for how to override them.

Once all three windows report they're up, open `http://localhost:5173` in a browser.

## First-time setup: creating your first user

There is no open self-registration endpoint. The **only** way to create a User is:

1. An existing Admin creates one (once you have at least one Admin), or
2. The **one-time setup-token bootstrap**, for creating that very first Admin.

The bootstrap path is `POST /api/setup/first-admin`, gated by a `TELECHRON_SETUP_TOKEN` value that must be configured on the Host. It only works while the Users table is empty — as soon as one User exists (including the one it just created), the endpoint permanently refuses to run again, regardless of token.

### Steps

1. **Set a setup token** before starting the Host. Pick your own value — it isn't provided for you, and there is no default. Set it as an environment variable:

   ```bat
   set TELECHRON_SETUP_TOKEN=choose-a-long-random-value-here
   ```

   (or add `"Telechron:SetupToken": "..."` to `Host/appsettings.Development.json`.)

   If you're using `start-all.bat`, set this variable before running it (or add the line into the script alongside the other `set` statements), since the Host reads it once at startup.

   If `TELECHRON_SETUP_TOKEN` is unset, `/api/setup/first-admin` is disabled entirely — a fresh deploy can't accidentally bootstrap itself.
2. **Start the Host** (via `start-all.bat`, or `dotnet run` from `Host/` directly).
3. **Open the Frontend** at `http://localhost:5173`. With zero Users in the database, the app detects this automatically (`GET /api/setup/status`) and shows a **Set up Telechron** form instead of the login screen. Fill in:

   - Setup Token — the value of `TELECHRON_SETUP_TOKEN`
   - Display Name
   - Email
   - Password (12+ characters)

   Submit, and the account is created as an Admin. You're redirected to the ordinary login screen.
4. **Log in** with the email/password you just chose. You'll land on the Dashboard.

If you'd rather do it without the UI:

```bash
curl -X POST http://localhost:5280/api/setup/first-admin \
  -H "Content-Type: application/json" \
  -d '{"setupToken":"choose-a-long-random-value-here","email":"you@example.com","password":"correct horse battery staple","displayName":"Your Name"}'
```

### After setup

The setup token has no further use once your first Admin exists — `/api/setup/first-admin` will return `409 Conflict` for any subsequent call. You can safely unset `TELECHRON_SETUP_TOKEN`, rotate it, or leave it in place (it's inert once a User exists); it's your call. Additional Users are created by an Admin through the normal authenticated API/UI, not through this endpoint.

## Configuration

The Host reads the following, from `Telechron:*` configuration keys or the listed environment variable (env var wins if both are absent from config, config wins if both are present):

| Setting                 | Config key                          | Env var                               | Notes                      |
| ----------------------- | ----------------------------------- | ------------------------------------- | -------------------------- |
| Setup token             | `Telechron:SetupToken`            | `TELECHRON_SETUP_TOKEN`             | Unset = bootstrap disabled |
| JWT signing key         | `Telechron:JwtSigningKey`         | `TELECHRON_JWT_SIGNING_KEY`         | HMAC-SHA256 key, 32+ bytes |
| Allowed CORS origins    | `Telechron:AllowedOrigins`        | —                                    | Comma-separated            |
| mTLS CA cert            | `Telechron:Mtls:CaCertPath`       | `TELECHRON_MTLS_CA_PATH`            |                            |
| mTLS Host cert (pfx)    | `Telechron:Mtls:HostCertPfxPath`  | `TELECHRON_MTLS_HOST_CERT_PATH`     |                            |
| mTLS Host cert password | `Telechron:Mtls:HostCertPassword` | `TELECHRON_MTLS_HOST_CERT_PASSWORD` |                            |
| Agent enrollment token  | `Telechron:AgentEnrollmentToken`  | `TELECHRON_AGENT_ENROLLMENT_TOKEN`  |                            |
| Data directory          | `Telechron:DataDirectory`         | —                                    | SQLite + other local state |

`start-all.bat` sets development values for most of these automatically. For anything beyond local development, set them explicitly rather than relying on the defaults baked into the launcher script.

## Running tests

```bash
dotnet test
```

Backend tests boot the real Host in-process (`WebApplicationFactory<Program>`) against a temporary data directory per test fixture, so they exercise the actual auth/RBAC/rate-limiting pipeline rather than mocks.
