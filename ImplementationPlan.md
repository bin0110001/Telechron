# Telechron — Implementation Plan & Checklist

> Companion to `TechDesign.md`. This is the build order. Every requirement ID (e.g. `R-SYS1`)
> refers to that document — the design is the source of truth; this file is the sequence.
>
> **How to use this (for the implementing model):**
> - Work top to bottom. A phase's exit criteria must pass before the next phase starts.
> - Each task lists the **requirement IDs** it satisfies, the **files/areas** it touches, and a
>   **done-when** check. Read the referenced requirement in `TechDesign.md` before implementing — do
>   not work from the summary line alone.
> - Every feature obeys the §9 definition-of-done: persists, self-tests, is repairable, container-safe,
>   has a UI surface, emits provenance/audit records, enforces permissions Host-side. Don't mark a task
>   done if it skips one of these where applicable.
> - The `~800 line` file cap (R-ENG1) applies to everything you write. Split proactively.
> - Windows + PowerShell is the dev host (R-ENG5). Prefer cross-platform paths and shell-agnostic scripts.
> - When unsure whether a security/permission behavior applies, assume it does and gate it.

**Legend:** `[ ]` todo · `[~]` in progress · `[x]` done · 🔒 security-critical (do not shortcut) · 🧱 load-bearing seam (many things depend on it)

---

## Phase 0 — Repository & Toolchain Foundation

Goal: a building, testable solution skeleton with CI, before any domain code.

- [x] 🧱 Create solution layout: `Host/`, `Agent/`, `Sdk/` (shared contracts), `Modules/` (sample + real modules), `Frontend/`, `Tests/`. Modules are **never** compile-time referenced by Host (R-NS1, R-SYS4).
  - **Done when:** `dotnet build` succeeds on an empty solution; Host has zero project reference to anything under `Modules/`.
- [x] Set up `Sdk/` project holding **only** shared contracts/interfaces (module contracts, function contracts, DTOs). No leaf logic here (R-ENG3).
  - **Done when:** Sdk compiles standalone and is referenced by Host, Agent, and sample module.
- [x] Establish test project(s) and a single `dotnet test` entrypoint. Every subsequent feature ships tests here or in its module (R-ENG6).
- [x] Add CI/lint scaffolding: build + test + a file-length lint that emits a warning (not a hard fail) at ~800 lines (R-ENG1). Wire it as a plain check for now; the "emits a Finding" behavior comes in Phase 6.
  - **Done when:** CI runs build+test+lint on push; oversized file produces a visible warning.
- [x] Decide and document the container runtime (Docker/Podman) and pin base image digests (R-SYS9). Record chosen registry allowlist.
  - **Done when:** a `containers/` dir holds digest-pinned base + one toolchain image definition, version-controlled.

**Exit criteria:** solution builds, tests run, CI green, container runtime chosen and images pinned. ✅ MET — see commit for Phase 0.

**Notes for next phases:**
- Solution file is `Telechron.slnx` (SDK 10.0.300 defaults to the new XML slnx format, not `.sln`).
- Chose **net10.0** as TFM (user decision) and **Podman** as container runtime (user decision — daemonless/rootless fits R-SYS7's isolation posture).
- Host webapi template pulled a vulnerable transitive `Microsoft.OpenApi` 2.0.0 (CVE-2026-49451); pinned explicitly to `2.7.5` (patched, same major — 3.x breaks `Microsoft.AspNetCore.OpenApi`'s source generator).
- `Frontend/` dir created but empty — real scaffolding starts in Phase 10 (built incrementally alongside backend phases per that phase's goal).
- Sdk currently holds only a marker type (`Telechron.Sdk.AssemblyInfo`); real contracts arrive in Phase 1/2 as entities are designed.

---

## Phase 1 — Persistence Core & Security-Prerequisite Entities 🧱

Goal: the minimum durable core the security floor needs — the DB, the repository/mapping seam, and **only** the entities security depends on (`User`, `Secret`, `Project`). The remaining ~14 domain entities are deferred to Phase 3, *after* the security seams exist, so security is never retrofitted onto them.

- [x] 🧱 Set up SQLite + EF Core with **WAL mode** and `busy_timeout`/backoff on `SQLITE_BUSY` from day one (R-PER1, R-PER6). Do not defer WAL — retrofitting write-contention handling is painful.
  - **Done when:** DB opens in WAL; a concurrent-write test shows retry/backoff rather than a hard failure.
- [x] 🧱 Establish the **repository interface** layer and the **domain-model ↔ DB-entity separation** (R-PER3, R-PER4). Domain models are POCOs; DB entities are separate; a mapping layer bridges them.
  - **Done when:** one entity round-trips through a repository with domain and DB types distinct.
- [x] Implement **only the security-prerequisite entities** (migration + repository + round-trip test each):
  - [x] `User` + Role (Viewer/Operator/Admin) + notification target + project membership (R-DM15). Needed by API auth/RBAC and audit attribution.
  - [x] `Secret` (R-DM12) — encrypted value column; referenced only by handle. Needed by the secret seams.
  - [x] `Project` (R-DM1) — incl. Repair Policy (`FullyAutonomous` / `RequireApproval`) and owner (User FK). Projects are the unit of trust/scope (R-DM1), so RBAC and secret scoping need it. *(Its FKs to later entities — Runs, Workflows, etc. — are added when those land in Phase 3.)*
- [x] Implement **corruption guard + backup/restore** (R-REL2, R-REL5): scheduled `VACUUM INTO` to a rotating retention set; corruption recovery restores from latest verified backup (never silently empties). Document RPO/RTO. *(Do it now: it protects the audit log and secrets store from the moment they exist.)*
  - **Done when:** a scheduled backup produces a restorable file; a simulated corruption restores rather than resets.

**Exit criteria:** DB in WAL with backoff; `User`/`Secret`/`Project` persist and round-trip through repositories with domain/DB separation; backup/restore proven. ✅ MET.

**Notes for next phases:**
- Domain POCOs live in `Sdk/Domain/` (user decision — shared with Agent/Modules later); DB entities + EF config + repositories live in `Host/Persistence/` (Host is sole DB owner per R-SYS1). `Host/Persistence/Mapping/*Mapper.cs` bridges the two via extension methods (`ToDomain()`/`ToEntity()`/`ApplyTo()`).
- WAL mode + `busy_timeout` (5000ms) are set via a `DbConnectionInterceptor` (`WalPragmaConnectionInterceptor`) on every connection open, since the Sqlite connection-string builder has no journal-mode property. EF Core's SQLite provider ships no built-in retrying execution strategy (that's SqlServer/Cosmos-only) — wrote a custom `SqliteRetryingExecutionStrategy` retrying on SQLITE_BUSY(5)/SQLITE_LOCKED(6) with exponential backoff, registered via `options.ReplaceService<IExecutionStrategyFactory, ...>`.
- **RPO/RTO** (documented here per R-REL5, no separate doc yet): default scheduled backup interval is 6h (`DatabaseBackupOptions.BackupInterval`) → RPO ≈ 6h worst case. Restore is a file copy after integrity verification → RTO ≈ seconds to low minutes depending on DB size. Retention: last 14 backups kept (`MaxRetainedBackups`), rotated oldest-first. Revisit both once real write volume is known; R-PER7 (Phase 3) will tune retention further.
- Backup uses `VACUUM INTO` (atomic, produces a compacted standalone file) + `PRAGMA integrity_check` verification before trusting a backup or restoring from one. Restore never falls back to an empty DB — `RestoreLatestVerifiedAsync` throws `InvalidOperationException` if no backup passes verification, by design (see R-FIX11 dependency on repair-lineage history survival).
- Gotcha for later phases touching SQLite files directly: `SqliteConnection` pools connections even after `CloseAsync()`, keeping the file handle open. Any code that needs exclusive file access after closing (restore, corruption simulation, etc.) must call `SqliteConnection.ClearPool(connection)` first.
- `Secret.EncryptedValue`/`EncryptionKeyId` are schema-only placeholders in Phase 1 — actual encryption-at-rest with an external key store, handle tokenization, and revocation semantics land in Phase 2 (R-SEC1, R-SEC8, R-SEC9). Don't assume the current bytes are meaningfully encrypted yet.
- Test project `Tests/Host.Persistence.Tests` uses real file-backed SQLite DBs (temp dir per test, cleaned up in `DisposeAsync`) rather than `:memory:`, since WAL mode and `VACUUM INTO` both need a real file to test meaningfully.

---

## Phase 2 — Security & Identity Seams 🔒🧱

Goal: the non-negotiable security floor, wired **before** the rest of the domain model and long before any code-generation or agentic feature. Retrofitting these is the failure mode we're avoiding. Everything from Phase 3 onward routes through the seams built here.

- [x] 🔒 **Secret encryption at rest with external key management** (R-SEC1, R-SEC9): encryption key comes from a platform key store / HSM / externally-supplied master key — **never** stored in the SQLite file or beside it. Support key rotation independent of secret rotation.
  - **Done when:** secrets encrypt/decrypt; the DB file alone (without the external key) cannot decrypt them; a key-rotation path exists.
- [x] 🔒 **Secret handle tokenization** (R-SEC1): Personas/prompts only ever see opaque handles. Host resolves handle→value at execution time, outside any LLM context.
  - **Done when:** no code path places a raw secret into a prompt; a test asserts prompts contain only handles.
- [x] 🔒 **Secret resolution boundary for agentic calls** (R-SEC5): raw secret injected only inside the Host/Connector runtime at the final hop; tool results returned to an LLM are scrubbed/re-tokenized before re-entering context.
  - **Done when:** a simulated agentic connector call authenticates without the secret ever appearing in tool-call args or returned tool output.
- [x] 🔒 **Secret lifecycle** (R-SEC8): rotation + immediate revocation; revocation invalidates outstanding handles and fails in-flight calls using the old value.
- [x] 🔒 **Human-facing API auth + RBAC** (R-SEC6): authenticated User sessions; per-Project RBAC (Viewer/Operator-Approver/Admin); rate limiting on mutating endpoints; input validation; CORS restricted to configured origins. Wire against the `User`/`Project` entities from Phase 1.
  - **Done when:** an unauthenticated request to a mutating endpoint is rejected; a Viewer cannot approve; CORS blocks a non-allowlisted origin.
- [x] 🔒 **Tamper-evident security audit log** (R-SEC7): append-only, hash-chained; stored **separately** from operational telemetry and outside the mutable R-PER1 path. Log: secret access, approval decisions (with approver identity), module installs, capability grants, auto-committed repairs. *(Build the store + hash-chain now; later phases just emit into it.)*
  - **Done when:** an audit entry cannot be edited/deleted through the normal DB path; the hash chain detects tampering.
- [x] 🔒🧱 **Host-side permission mediation primitive** (R-MOD8a): a single non-bypassable authorization check at dispatch time for all Persona tool/connector/workflow/secret access and all module capability use. LLM self-restraint is never the enforcement. This is the seam every later capability calls — build it as a general primitive even though its callers (Personas, modules) don't exist yet.
  - **Done when:** the primitive rejects an access request against a supplied allowlist; a placeholder caller test proves deny-by-default; a test proves it cannot be bypassed by prompt content.
- [x] 🔒 **Secret redaction from operational logs** (R-SEC1).

**Exit criteria:** secrets safe at rest with external keys; handles never leak to prompts; human API authenticated + RBAC-gated; audit log tamper-evident; permission mediation is a callable Host primitive everything else will route through. ✅ MET.

**Notes for next phases:**
- All Phase 2 code lives under `Host/Security/` (`Secrets/`, `Audit/`, `Auth/`, `Permissions/`, `Logging/`), contracts in `Sdk/Security/` (`ISecretVault`, `ISecretResolutionScope`, `IAuditLog`, `IPermissionMediator`, `IMasterKeyProvider`, `ISecretEncryptionService`, `ISecretFingerprintRegistry`). 50 tests in `Tests/Host.Security.Tests`.
- **Secret encryption**: envelope encryption (AES-256-GCM) — each Secret gets a random per-secret DEK; the DEK is wrapped with the current master key (KEK) and stored alongside the value ciphertext in `Secret.EncryptedValue` (binary layout documented in `AesGcmSecretEncryptionService`). `EncryptionKeyId` tracks which KEK wrapped the DEK, so KEK rotation only requires rewrapping DEKs, never re-encrypting values. KEK itself never touches SQLite — sourced from `TELECHRON_MASTER_KEY` (base64) or `TELECHRON_MASTER_KEY_FILE` env vars; `TELECHRON_MASTER_KEY_ID` names the active version, `TELECHRON_MASTER_KEY_{oldId}` supplies retired versions for decrypting not-yet-rewrapped DEKs.
- **Audit log**: physically separate SQLite file (`telechron-audit.db` vs `telechron.db`), hash-chained via `RecordHash = SHA256(PriorHash || canonical fields)`. `IAuditLog` exposes no Update/Delete — that's the tamper-evidence guarantee by construction; `VerifyChainAsync` detects any out-of-band edit/delete by recomputing the chain.
- **Human API auth**: JWT bearer (user decision), signing key via `TELECHRON_JWT_SIGNING_KEY` env var or `Telechron:JwtSigningKey` config (config-first so `WebApplicationFactory`-based integration tests can override it cleanly). Project-scoped RBAC via a custom `ProjectRoleRequirement`/`ProjectRoleAuthorizationHandler` reading a route value named `projectId` — **any future controller route needing project-scoped authorization must name its route parameter `projectId`** for the handler to find it. Global `Role.Admin` bypasses project-scope checks entirely.
- Password hashing uses `Microsoft.AspNetCore.Identity`'s `PasswordHasher<T>` (framework-provided via the Web SDK — do not add the NuGet package explicitly, it's already in the shared framework and adding it produces an NU1510 prune warning).
- CORS default-denies (no origins configured = no origins allowed) rather than falling back to `AllowAnyOrigin` — configure via `Telechron:AllowedOrigins` (comma-separated).
- Rate limiting: two named policies (`RateLimiting.AuthPolicyName`, `RateLimiting.MutatingPolicyName`) — apply `[EnableRateLimiting(...)]` on any new mutating/auth-adjacent endpoint.
- **Permission mediator** (`PermissionMediator`) is a pure function of `(CapabilityRequest, allowlist)` — it never consults anything LLM/prompt-derived, which is what makes "cannot be bypassed by prompt content" true by construction rather than by convention. Every Persona/module capability check in later phases must route through `IPermissionMediator.AuthorizeAsync`, never re-implement an allowlist check inline (that would violate R-ENG4's spirit and reopen the bypass risk).
- **Log redaction**: primary control is discipline (never call `ILogger` with raw secret bytes); `ISecretFingerprintRegistry`/`RedactingLoggerProvider` are the safety net, tracking a resolved secret's plaintext only for the lifetime of its `ISecretResolutionScope.ExecuteAsync` call. Only wraps the console provider currently — if additional sinks (file, OTLP, etc.) are added in later phases, wrap them the same way.
- Gotcha: `SecretVault`/`ProjectMembership`/`Secret` all have real FKs to `Project`/`User` (from Phase 1) — any test creating a Secret or ProjectMembership needs a real seeded Project, not a bare `Guid.NewGuid()`. See `Tests/Host.Security.Tests/Fixtures/ProjectSeeding.cs`.
- A minimal `DiagnosticsController` (`/api/diagnostics/whoami`, `/operator-only`, `/admin-only`) exists purely to exercise the RBAC seam end-to-end before real Phase 3+ controllers land — expect it to be superseded, not extended.

---

## Phase 3 — Remaining Domain Model 🧱

Goal: the rest of the persisted entities, built on the secured core. Each is created with its security seams already available (RBAC scoping, audit hooks, permission mediation), so none needs a retrofit.

- [x] Implement remaining domain entities as persisted aggregates. **One entity per task-ish; group trivial ones.** Each needs migration + repository + round-trip test, and wires its Project FKs back to Phase 1's `Project`:
  - [x] `Run` + full lifecycle states (R-DM2): Pending/Running/Passed/Failed/Cancelled/TimedOut/Stalled.
  - [x] `Finding` (R-DM3) — incl. **Failure Class (Environment vs Code)** field (R-FIX8) and **Provenance** back-refs (R-DM3a).
  - [x] `RepairAttempt` (R-DM3a) — many-to-many to Findings; snapshot ref, patch, verify result, approver, resulting artifact/commit + provenance ref.
  - [x] `Function` (R-DM4) — incl. deprecation flag (R-DM7a).
  - [x] `Workflow` + `WorkflowRun` — **WorkflowRun lifecycle** (Pending/Running/AwaitingApproval/PartiallyFailed/Passed/Failed/Cancelled/TimedOut) and **definition pinning** snapshot (R-DM5, R-WF4).
  - [x] `Persona` (R-DM6) — allowed tools/connectors/workflows, max iterations, max cost, approval policies, allowed secrets (by handle). Its allowlists feed the Phase 2 mediation primitive.
  - [x] `Module` (R-DM7) + version fields, semver (R-DM7a).
  - [x] `Machine` + `Resource` (R-DM8) — incl. mutually-exclusive resource groups.
  - [x] `IntentPlan` (R-DM9) — side-effect-free proposal.
  - [x] `LlmConnection` (R-DM10).
  - [x] `Connector` (R-DM11) — incl. deprecation flag (R-DM7a); reusable across projects.
  - [x] `Artifact` (R-DM13) — **metadata/reference only in DB; binary payload lives outside SQLite** (R-PER7).
  - [x] `Toolchain` (R-DM14).
  - [x] `DesignDocument` + `Requirement` (R-DM16) — versioned revisions (not overwrites), Requirement entries with stable `R-XXX` IDs, status (Active/Superseded/Deprecated), Project FK. **Seed Telechron's own Design Document from `TechDesign.md` + `ImplementationPlan.md`** (R-DM16a) so the reflexive self-repair path has real content from day one instead of an empty placeholder.
- [x] Implement **binary Artifact blob storage** outside SQLite (filesystem or blob store), DB holds references only (R-PER7).
  - **Done when:** storing a large artifact does not grow the SQLite file; only metadata is in the DB.
- [x] Implement **retention policy** scaffolding for Runs/Findings/logs/LLM records with archival-before-delete; exempt repair-lineage data (R-PER7).
  - **Done when:** a retention pass archives+prunes old rows but leaves repair-lineage intact.

**Exit criteria:** all remaining entities persist and round-trip; artifacts stored out-of-DB; retention pass works; every entity's access is already RBAC-scopable and audit-hookable. ✅ MET.

**Notes for next phases:**
- 15 new domain entities landed (Machine, Resource, LlmConnection, Toolchain, Function, Connector, Module, Run, Persona, Workflow, WorkflowRun, Finding, IntentPlan, Artifact, RepairAttempt) plus the R-DM16 trio (DesignDocument, Requirement, RequirementRevision) — 19 new tables total (18 entities + the RepairAttempt↔Finding join table). One consolidated migration (`Phase3DomainEntities`).
- **Cross-entity FK policy this phase established**: some entities (Function, Toolchain, Connector) intentionally store a `ModuleId` as a plain `Guid` with no EF navigation FK to `ModuleEntity` — Module has no inbound relationships from other entities yet, so this avoids premature coupling. Add the real FK in whatever later phase actually needs to join through it (Phase 5/6 module runtime).
- **Design Document reflexive seeding** (R-DM16a) lives in `Host/DesignDocuments/`: `MarkdownRequirementParser` extracts `R-XXX` blocks from `TechDesign.md` (handles both `R-XXX — Title` and bare `R-XXX` header forms — the one reliable body-boundary rule is "next `R-XXX` header line", since blank lines and bullet-style content appear inside real requirement bodies). `ReflexiveDesignDocumentSeeder` runs idempotently on every Host startup: creates a well-known `"Telechron"` system Project (owned by a non-loginable `system@telechron.internal` User) + its DesignDocument on first run, then upserts Requirements — unchanged text is a no-op, changed text creates a new `RequirementRevision` (never overwrites, per R-DM16b) and bumps `CurrentRevisionNumber`. Verified against the real `TechDesign.md`: 121 requirements parsed correctly. `ImplementationPlan.md` is NOT parsed into Requirement rows (no `R-XXX` IDs of its own) — it's process/checklist content, not itself a source of intent statements.
- **Retention pass gotcha**: the SQLite EF Core provider (10.0.10) cannot translate `DateTimeOffset` relational comparisons (`<`, `>`) in LINQ — neither direct comparison nor `.ToUnixTimeMilliseconds()` translates. `RetentionPass` works around this by materializing the (bounded) table with `ToListAsync()` first and filtering/ordering by date client-side. Fine for a periodic batch job at this scale; revisit if Run/Finding volume grows large enough for this to matter (R-PER7's own framing — "unbounded growth is the normal operating loop" — means this may need a real translatable comparison or raw SQL eventually).
- Retention scaffolding covers `Run` and `Finding` only (the two entities the plan named) — LLM call records don't exist as an entity yet (Phase 6), logs aren't a queryable table (they're blob-referenced), so both are deferred to whenever those land. `RetentionPolicy` (age+count) and `IRetentionArchive` (JSON Lines, filesystem, same directory family as the Artifact blob store) are designed to be reused as-is when that happens.
- `FilesystemArtifactBlobStore` and `FilesystemRetentionArchive` both live under `Host/Persistence/` — GUID-prefixed two-level directory split for blobs (avoids huge flat dirs), path-traversal guard on read (blobRef values originate from DB rows, treated as untrusted input).
- Test project `Tests/Host.Persistence.Tests/Phase3/` holds round-trip tests grouped by FK-dependency tier (matching the batches used during implementation); `Phase3Seeding.cs` is the shared FK-seeding helper (User→Project, Machine, LlmConnection, Workflow→WorkflowRun, Run, Finding).
- A parallel Workflow run was attempted for the bulk entity implementation and hit a session usage limit mid-flight; 4 of 14 dispatched entity agents (Function, LlmConnection, Machine, Toolchain) completed and their output was validated + kept, the remaining 10 plus DesignDocument/Requirement were implemented directly. No workflow orchestration issue to flag — just a session budget constraint on that attempt.

---

## Phase 4 — Agent, Transport & Container Execution 🔒🧱

Goal: authenticated workers that run untrusted code inside a hardened container boundary.

- [ ] 🔒🧱 **Agent registration + auth** (R-SEC2, R-SCH3): mTLS or signed tokens; registration requires authentication + dedup.
  - **Done when:** an unauthenticated agent is rejected; a duplicate registration is deduped.
- [ ] 🔒 **Command dispatch schema validation + parameter escaping** (R-SEC2) to prevent injection into agent commands.
  - **Done when:** a malformed/injection-crafted command is rejected by schema validation before execution.
- [ ] 🔒🧱 **Container execution boundary** (R-SYS6): all synthesized/module/untrusted code, self-tests, repair verification, and workflow execution run in containers. ALC/process-wrapping is lifecycle-only, not security.
- [ ] 🔒 **Untrusted container resource + network policy** (R-SYS7): CPU/memory/disk quotas; default-deny egress (allowlist per declared need); containers cannot reach the Host management plane or sibling Agents.
  - **Done when:** a container hits a memory cap and is killed; an un-allowlisted egress attempt is blocked; the container cannot reach the Host API.
- [ ] 🔒 **Container image provenance** (R-SYS9): images pinned by digest, from allowlisted registry, CVE-rescanned; build defs version-controlled.
- [ ] 🔒 **GPU access isolation** (R-SYS8): GPU-requesting untrusted containers only on dedicated single-tenant GPU agents; GPU memory cleared between tenants; GPU access follows the R-MOD8 approval path.
- [ ] **Warm container pools + layer caching** (R-SYS10): pools keyed by (Toolchain, dependency fingerprint); layer cache with invalidate-on-change TTL. *(Can start naive/cold and optimize; do not weaken R-SYS6/R-SYS7 for speed.)*
  - **Done when:** repeat verify of the same toolchain reuses a warm container and is measurably faster.
- [ ] **Heartbeats + stalled-run watchdog** (R-RUN3, R-REL1) with a **grace/reconnect window** before marking Stalled; resume-on-reconnect (R-SCH5).

**Exit criteria:** an authenticated agent runs a trivial workload inside a resource-limited, network-restricted, digest-pinned container; disconnect→reconnect resumes within the grace window.

---

## Phase 5 — Module Runtime & Hot Reload 🧱

Goal: the plugin system the whole "modularize everything" north-star rests on.

- [ ] 🧱 **Module runtime + loader** via isolated `AssemblyLoadContext` (R-SYS4, R-MOD7). Host accesses modules only through the runtime, never as compile-time deps.
- [ ] **Unified self-test contract** (R-MOD4) + **self-test falsifiability** (R-MOD4a): Verify confirms the self-test *fails* on the pre-patch snapshot before accepting a post-patch pass (negative control required).
  - **Done when:** a module whose self-test trivially passes on broken code is rejected by Verify.
- [ ] 🔒 **Module supply-chain integrity** (R-MOD5a): signature + checksum verified before install; refuse on mismatch. Self-tests are functional evidence only, never a security attestation.
- [ ] 🔒 **Pre-trust module sandboxing** (R-MOD5b): new/updated modules first run maximally restricted (network+capability denied beyond self-test) with observed behavior compared to declared R-MOD8 capabilities before capabilities take effect.
- [ ] **Capability permissions** (R-MOD8): modules declare required capabilities; Projects approve them; enforced via the Phase 2 mediation primitive (R-MOD8a).
- [ ] 🧱 **Two-phase drain + canary + ALC-leak guard** (R-MOD6, R-MOD6a): phase 1 stop new dispatch while in-flight completes/times out; phase 2 unload only at zero refs; post-reload canary window with auto-rollback on elevated errors; track ALC unload success and alert on retained-reference leaks.
  - **Done when:** a hot-reload with in-flight work drains cleanly; a broken new version auto-rolls-back; repeated reload cycles show no unload leak.
- [ ] **Module health monitoring** (R-MOD1) and **module versioning/compatibility** (R-DM7a): semver; same-major rebinds transparently; differing major requires re-approval; typed-artifact contracts stable within a major.
- [ ] Ship a **sample end-to-end module** (source + self-test + permissions) proving the whole contract.

**Exit criteria:** a signed, sandboxed sample module installs, runs its (falsifiable) self-test in a container, hot-reloads with drain, and rolls back when broken — all mediated by Host-side permissions.

---

## Phase 6 — Provider Modules: Runners, Toolchains, Functions, Connectors, LLM

Goal: the first real capabilities, each as a module (R-MOD2). These are largely parallelizable once Phase 5 holds.

- [ ] **Test runner module(s)** (R-RUN1, R-RUN2, R-RUN5): pluggable runners executing in containers against a Toolchain; warnings/info are not failures (R-RUN4).
- [ ] **Toolchain modules** (R-DM14, R-RUN5): build/test/verify/export/deploy commands + env requirements. Start with one (e.g. .NET) end-to-end, then add others (Godot, Node, Python…) as clones.
- [ ] **Function executor modules** (R-DM4, R-WF1, R-WF2): Run/Build/Git/Zip/Upload/Convert/Deploy etc. Hot-reloadable.
- [ ] **Connector modules** (R-DM11, R-MOD9): declare auth mechanisms, required secrets, supported artifact types + operations. Start with 1–2 (e.g. GitHub, filesystem/SSH) exercising the R-SEC5 secret boundary.
- [ ] **LLM provider registry + engine modules** (R-LLM1, R-LLM2): providers resolved through a registry; connection owns settings.
- [ ] 🔒 **LLM call tracking** (R-LLM3): every call records provider/model/tokens/cost/prompt metadata and surfaces in UI.
- [ ] 🔒 **Global + per-project LLM spend caps** (R-LLM4): rolling-window circuit breaker independent of per-repair/per-persona caps.
- [ ] 🔒 **Untrusted content isolation in prompts** (R-LLM5): Finding/connector free-text wrapped as inert data; reduced-permission profile when a Persona processes untrusted content.
  - **Done when:** a Finding containing injected instructions does not cause the repair Persona to act on them; a test proves the reduced-permission profile is applied.

**Exit criteria:** a container-run test suite against a real Toolchain produces results; a Connector authenticates via the secret boundary; an LLM call is tracked, capped, and processes untrusted content safely.

---

## Phase 7 — Findings & The Single Repair Pipeline 🔒🧱

Goal: the one generic repair loop (R-NS2). **No bespoke fix/verify/revert paths anywhere** (R-ENG4).

- [ ] 🧱 **Findings generation** from Runs and workflow failures (R-FIX1); the file-length lint from Phase 0 now emits a code-quality Finding on violation (R-ENG1).
- [ ] 🔒 **Environment vs Code classification** (R-FIX8): only Code-classified Findings become repair candidates; Environment (Stalled/TimedOut/network blips) route to retry/quarantine. Same mechanism as R-BUILD4 (do not duplicate).
- [ ] 🧱 **The generic pipeline** (R-NS2, R-FIX2): Snapshot → Generate Fix → Apply → Verify (in container) → Approval Gate (if required) → Revert on failure → Commit/Hot-Reload on success.
- [ ] 🔒 **Design Document as standing repair context** (R-DM6a, R-FIX2): Generate Fix always injects the Project's active Design Document (R-DM16) revisions alongside the Finding and source snapshot — not an optional tool call.
- [ ] 🔒 **Architectural drift detection** (R-FIX13): Verify checks the patch against the Requirement entries it touches/is tagged against; a patch that satisfies its Finding but contradicts an Active Requirement becomes a Drift Finding and forces RequireApproval regardless of policy (same privileged-path treatment as R-SEC4).
  - **Done when:** a patch engineered to pass its test while violating a stated Requirement is caught as a Drift Finding rather than auto-committed.
- [ ] **Deterministic-before-LLM** ordering (R-FIX5); deterministic fixes may use the R-SYS10 fast-path.
- [ ] **Repair routing by Finding origin** (R-FIX4) and **atomic multi-file patch transactions** (R-FIX7).
- [ ] **Bounds & governance** (R-FIX3): attempt caps, per-repair cost caps, decline short-circuiting, cross-run dedup; Project Repair Policy gate (RequireApproval pauses verified patches).
- [ ] **Bounded rescanning** (R-FIX6): rescan only patched files, capped against the same attempt cap.
- [ ] 🔒 **Repair concurrency & locking** (R-FIX9): exclusive locks per target; repair vs module hot-reload drain mutually exclude on module ID.
- [ ] 🔒 **Oscillation/regression detection** (R-FIX11): per-file diff-signature history; a fix matching a prior reverted patch short-circuits to RequireApproval.
- [ ] 🔒 **Repair-triggered synthesis routes through the human gate** (R-FIX10, R-NS3): if a fix needs a *new* capability/module, force R-BUILD5 approval regardless of policy.
- [ ] 🔒 **Privileged-path change control** (R-SEC4): patches touching permission/Persona/approval/secret/repair-pipeline/trust-policy code, **or a Project's Design Document** (R-DM16b), force RequireApproval + a distinct privileged-diff review surface — regardless of policy.
- [ ] 🔒 **Repair diff scope limits** (R-FIX12): oversized or out-of-origin patches require elevated review.
- [ ] 🔒 **Repair provenance & commit attestation** (R-SEC3): every auto-commit/hot-reload carries a signed, immutable provenance record (source Finding, Persona, model version, verify results), stored independently. Populates the RepairAttempt (R-DM3a).
- [ ] **Repair Plan batch aggregation** (R-FIX2a): batch scans aggregate related Findings under one Approval Gate; shares provenance + diff-scope rules.

**Exit criteria:** a failing containerized test becomes a Code Finding, flows through the single pipeline, verifies in a container, respects the policy gate, and commits with a signed provenance record — while a privileged-path or synthesis-requiring or oscillating fix is forced to human approval.

---

## Phase 8 — Workflows, Functions & Intent Planning

Goal: composition and the natural-language front door.

- [ ] **Workflow engine** (R-WF1): function-driven; linear + graph execution; variables; typed artifacts (R-DM5, R-WF6).
- [ ] **Typed artifact passing** (R-WF6): steps declare input/output artifact types.
- [ ] **Approval gates** (R-WF5): automatic / manual approval / manual edits / multi-stage; execution pauses (AwaitingApproval state) until satisfied. Approver identity recorded (R-DM15, R-SEC7).
- [ ] **Failure policies** (R-WF3) driving WorkflowRun aggregate status (FailFast→Failed, ContinueOnError→PartiallyFailed).
- [ ] **Durable WorkflowRuns across restart** (R-WF4) with definition-pinning snapshot (Phase 3).
- [ ] **Intent planning** (R-BUILD1, R-BUILD2, R-DM9): deterministic when a rule/pattern matches, else Persona/LLM fallback; plans are side-effect-free; record which path produced the plan.
- [ ] 🔒 **Capability gap approval flow** (R-BUILD5, R-BUILD3, R-BUILD4): NL → Intent Plan → Gap Analysis → **Human Approval** → Synthesis (source + self-test + module) → Container Verification → Install → Workflow Gen → Execution. NL never directly synthesizes/installs. Environment vs code failures distinguished during synthesis.
  - **Done when:** an NL request needing a missing capability cannot install it without passing the human gate; synthesized capability is capped at the requesting Persona's permissions (R-MOD8a).
- [ ] 🔒 **Design Document consultation during synthesis** (R-BUILD3, R-DM6a): Capability Synthesis receives the Project's active Design Document as standing context; Container Verification runs the same drift check as R-FIX13 — a synthesized capability that contradicts an Active Requirement is flagged as a Drift Finding at the R-BUILD5 Human Approval gate rather than installed.
- [ ] 🔒 **Design Document edit approval flow** (R-DM16b): Design Document revisions (whether proposed by Repair/Synthesis or edited directly) route through the same privileged-path Human Approval gate as R-SEC4; a revision only becomes Active after approval. A human may also edit directly.
- [ ] **Reflexive self-application** (R-DM16a): wire Telechron's own Design Document (seeded in Phase 3 from `TechDesign.md`/`ImplementationPlan.md`) into the Host Sentinel's self-repair loop (R-REL3, Phase 9) via the same mechanism above — no special-cased path.
- [ ] **Personas as the single editable home** for repair/planning/synthesis/generation logic (R-DM6); enforced by mediation (R-MOD8a) and reduced-permission profiles (R-LLM5).

**Exit criteria:** an NL request produces a side-effect-free plan; approval drives synthesis→verify→install→workflow; a graph workflow with an approval gate runs durably across a restart with correct aggregate status; a synthesized capability that contradicts the Design Document is caught as a Drift Finding, not installed.

---

## Phase 9 — Scheduling, Resources & Reliability

Goal: continuous, fair, observable operation.

- [ ] **Scheduling** (R-SCH1, R-SCH4): runs serialize per machine, workflows per project; scheduled executions are WorkflowRuns; durable and DB-failure-resistant.
- [ ] **Exclusive resource groups** (R-SCH2, R-DM8): mutually-exclusive resources enforced.
- [ ] **Priority, starvation prevention, autoscaling hooks, reconnect** (R-SCH5): priority classes with aging; grace/reconnect resume; queue-depth autoscaling hooks.
- [ ] **Host Sentinel repair loop** (R-REL3) — self-repair of the repair engine is a privileged path → always RequireApproval (R-SEC4). Consults Telechron's own Design Document via the Phase 8 reflexive wiring (R-DM16a) exactly as any managed Project's repair Persona would.
- [ ] **Host scaling ceiling + migration path** (R-REL4): publish documented max agents/workflows/write-throughput and the SQLite→networked-RDBMS trigger; agents buffer telemetry across a Host outage; bounded restart-recovery.
- [ ] 🧱 **Distributed tracing + health/readiness endpoints** (R-REL6): correlation/trace ID propagated Host→Agent→Container→persistence; liveness/readiness endpoints; watchdog + sentinel consume metrics + alerting thresholds.
- [ ] **High-frequency telemetry batching** (R-PER5) — confirm it bypasses direct SQLite writes.

**Exit criteria:** scheduled workflows run durably; resource exclusivity holds; a traced request is followable end-to-end across all hops; health endpoints report correctly.

---

## Phase 10 — Frontend (Parity) 🧱

Goal: R-SYS3/R-UI1 mandate a UI surface for **every** backend capability. Build incrementally alongside each backend phase where possible; this phase is the completeness sweep.

- [ ] **Feature-first SPA architecture** (R-UI4) with **realtime updates across all active surfaces** (R-UI3), authenticated via Phase 2 sessions/RBAC.
- [ ] Required surfaces (R-UI2) — each is a task: Runs · Work Queue · Projects · Workflows (with graph editor) · Machines · Resources · Modules · Connectors · Toolchains · LLM Configurations · Assistant · Storefront · Scheduling · Secrets Management · **Design Document** (Requirement list/detail, revision history, propose/approve edit diff, Drift Finding views).
- [ ] Security-specific surfaces implied by the new requirements: **privileged-diff review** (R-SEC4/R-FIX12), **approval queue with approver attribution** (R-WF5/R-DM15), **audit-log viewer** (R-SEC7), **LLM cost/spend dashboard** (R-LLM3/R-LLM4), **provenance / "why did this change" view** (R-SEC3/R-DM3a), **Drift Finding review** (R-FIX13 — surfaces alongside privileged-diff review since it shares the same approval gate).
- [ ] Parity audit: enumerate every backend capability and confirm a corresponding surface exists (R-UI1). No capability ships UI-less.

**Exit criteria:** every capability is reachable and controllable from the UI with realtime updates; security review/approval/audit surfaces exist.

---

## Phase 11 — Storefront (Optional) & Hardening

- [ ] **Storefront** (R-SYS5): out-of-process catalog, **disabled by default**, governed by project trust policies; acquisition honors R-MOD5a signing + R-MOD5b pre-trust sandboxing.
- [ ] Full pass against the **§9 acceptance tests** — treat each bullet as a gate:
  - [ ] Register agent (auth) → run containerized suite → stream realtime telemetry.
  - [ ] Failing tests → Findings w/ Repair Context → repair via the one pipeline.
  - [ ] NL request → deterministic plan → approved synthesis → workflow execution.
  - [ ] Hot-reload via drain + ALC isolation.
  - [ ] Scheduled workflows (test/maintenance/deploy/vuln remediation).
  - [ ] Toolchains-in-containers for real builds (Godot/Unity/.NET).
  - [ ] Connectors + secret-handle tokenization for external calls.
  - [ ] Typed artifacts passed between steps.
  - [ ] Module capability permissions + sandbox restrictions enforced.
  - [ ] Every capability surfaced in the frontend.
- [ ] **Definition-of-done sweep** (§9): for each shipped feature confirm it persists, self-tests, is repairable, container-safe, has UI, emits provenance/audit, enforces permissions Host-side, distinguishes env-vs-code failures, and uses the single repair pipeline where applicable.

**Exit criteria:** all §9 acceptance tests pass on a fresh install; the definition-of-done sweep is clean.

---

## Cross-Cutting Rules (apply in every phase)

- **One repair pipeline only** (R-NS2, R-ENG4) — never write a second fix/verify/revert path.
- **Modularize leaf capabilities** (R-NS1, R-ENG2); **shared logic in the SDK** (R-ENG3).
- **~800-line file cap** (R-ENG1); split proactively.
- **Windows/shell hygiene** (R-ENG5).
- **Every feature ships tests** (R-ENG6) and its **UI surface** (R-UI1).
- **Security defaults to on:** if unsure whether to gate/audit/mediate, do it.
- **Persist anything that survives restart** (R-PER2).
- **Repair/synthesis is intent-aware, not source-only** (R-NS4, R-DM6a) — once Phase 8 lands, every repair/planning/synthesis Persona must receive the Design Document as standing context, and Design Document edits are always a privileged-path human approval (R-DM16b), never autonomous.

---

## Suggested Build Order Rationale (for the implementer)

1. **0** is the bare solution/CI scaffold — nothing exists without it.
2. **1** is deliberately *lean*: only the DB, the repository seam, and the three entities security cannot function without (`User`, `Secret`, `Project`) plus backup/restore. It is not "all persistence" — that's Phase 3.
3. **2 (security) comes immediately after**, as early as it can possibly go given real dependencies (it needs a DB and those three entities to exist, nothing more). Secret encryption/tokenization, human API auth+RBAC, the tamper-evident audit log, and the Host-side permission-mediation primitive are all built here — **before** the rest of the domain model, before agents, before modules, before any LLM or code-execution feature. Every later phase calls into these seams instead of bolting security on afterward. This is the retrofit we are explicitly avoiding.
4. **3** fills in the remaining ~14 domain entities now that they can be built RBAC-scoped and audit-hookable from day one.
5. **4–5 (agent/containers/modules)** are the execution + plugin substrate the north-star depends on, and lean on Phase 2's permission mediation and container/GPU/image-provenance rules immediately.
6. **6 (provider modules)** are the first real capabilities and are parallelizable; Connectors exercise the Phase 2 secret-resolution boundary directly.
7. **7 (repair pipeline)** is the heart; it needs everything above it, including the privileged-path routing and provenance seams from Phase 2.
8. **8–9 (workflows/planning/scheduling/reliability)** compose and operationalize.
9. **10 (frontend)** is parity — build surfaces alongside their backends, sweep here. Every surface is auth-gated from Phase 2 by construction.
10. **11 (storefront/hardening)** is optional + the final acceptance gate.
