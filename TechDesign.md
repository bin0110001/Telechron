
0. What Telechron Is (North Star)
   Telechron is a self-repairing, self-building task engine. A user describes an outcome in natural language; the system assembles whatever projects, functions, workflows, and modules are needed to produce it — and when any part breaks, it repairs itself. Unlike most autonomous coding systems, it repairs and builds against a living record of intent, not source code alone — so it can tell the difference between "matches what the tests check" and "matches what the software is supposed to do" (R-NS4).

   (Naming: the canonical spelling is "Telechron", matching the repository. Earlier drafts used "Telochron"; treat the two as the same system.)

Every requirement below is judged by one test: does it make the system easier to auto-build and auto-repair?

Two derived principles constrain the whole architecture:

R-NS1 — Modularize Everything

Every leaf capability (a runner, deployment target, function executor, connector, toolchain, LLM provider, or repair engine itself) must live in a hot-reloadable module that ships its own source code and self-test.

Code compiled into the Host skeleton cannot be auto-repaired.

The Host holds only orchestration, persistence, and cross-cutting seams.

R-NS2 — One Repair Loop, Not Many

Repair must converge on a single generic pipeline:

Snapshot
→ Generate Fix
→ Apply
→ Verify (Build + Self-Test)
→ Revert on Failure
→ Hot Reload / Commit on Success
No capability may introduce a bespoke fix/verify/revert path when the generic one can be extended.

R-NS3 — Autonomy Is Bounded to Patching, Never to Self-Expansion of Privilege

A Project's FullyAutonomous repair policy authorizes autonomous patch-and-commit of *existing* code only. It never authorizes: installing/synthesizing new capabilities or modules (that always passes R-BUILD5's human gate — see R-FIX10), modifying the permission, Persona, approval-gate, secret-handling, or repair-pipeline code itself (that always routes to RequireApproval — see R-SEC4), or granting a capability the triggering Persona does not already hold (see R-SEC9). "Fully autonomous" is bounded self-repair, not unbounded self-modification.

R-NS4 — Repair and Synthesis Are Intent-Aware, Not Source-Only

Most autonomous coding systems reason only from source code and test outcomes — they know what a system *does*, never what it was *meant* to do. Telechron holds a living Design Document (R-DM16) per Project — the same role `TechDesign.md`/`ImplementationPlan.md` play for Telechron itself (R-DM16a) — and every repair, planning, and synthesis Persona receives it as standing context (R-DM6a), not an optional lookup. A fix that makes tests pass but contradicts stated architectural intent is a Drift Finding, not a success (R-FIX13). Because an agent rewriting its own spec to match whatever it already built would defeat the entire point, Design Document edits are themselves a privileged path requiring human approval (R-DM16b, R-SEC4) — intent can only drift on purpose, with a human signing off, never silently.

1. System Shape (The Fixed Skeleton)
   R-SYS1 — Host
   A single ASP.NET Core service that owns:

Persistence

Run orchestration

Workflow orchestration

Module runtime

Scheduling

REST API

Realtime API

The Host is the only component with a database.

R-SYS2 — Agent
A lightweight per-machine service that:

Authenticates with the Host via mTLS or signed registration tokens

Advertises capabilities and hardware

Executes dispatched work inside container boundaries

Examples:

Run tests

Build projects

Execute workflows

Repair code

Manage GPU resources

Agents are stateless workers.

R-SYS3 — Frontend
A single-page web application. Every backend capability MUST have a corresponding UI surface. No feature is considered complete without UI support.

R-SYS4 — Modules
Hot-reloadable plugins loaded at runtime.

Modules are never compile-time dependencies of the Host and are only accessed through the module runtime.

R-SYS5 — Storefront (Optional)
An out-of-process catalog that may provide:

Modules

Toolchains

Connectors

Function packs

Disabled by default and governed by project trust policies.

R-SYS6 — Container Execution Isolation Boundary
All synthesized code, module code, untrusted execution, module self-tests, repair verification, and workflow executions MUST execute inside containerized environments.

Assembly unloading via AssemblyLoadContext or process wrapping is used for runtime lifecycle management only, not for security sandboxing. Containers provide the hard isolation boundary across Host and Agent nodes.

R-SYS7 — Untrusted Container Resource & Network Policy
Containers executing synthesized, untrusted, or module code MUST enforce CPU, memory, and disk quotas and default-deny network egress (allowlist only for declared Connector/Toolchain needs). Such containers MUST NOT be network-reachable to the Host management plane (REST/Realtime API, DB) or to sibling Agents. This closes the "isolation boundary" gap where reachable-but-unescaped containers could still attack the control plane without any container-escape exploit.

R-SYS8 — GPU Access Isolation
GPU passthrough weakens container isolation. Untrusted/synthesized-code containers requesting GPU capability (R-DM8) run on dedicated, single-tenant GPU-enabled Agents with no co-scheduled trusted workloads, GPU memory is cleared between tenants, and GPU access requires the same capability-approval path as R-MOD8.

R-SYS9 — Container Image Provenance
All execution/verification container images (base OS, Toolchain images per R-DM14) are pinned by digest — never mutable tags — sourced from an allowlisted registry, and periodically CVE-rescanned. Image build definitions are version-controlled alongside the Toolchain module that references them.

R-SYS10 — Warm Container Pools & Layer Caching
To keep repair-verify cycles fast (the north-star goal), Agents maintain warm pools of pre-provisioned containers keyed by (Toolchain, project dependency fingerprint), and cache build/dependency layers across repair attempts within a bounded, invalidate-on-change TTL. Deterministic non-LLM fixes (R-FIX5) may reuse a pre-warmed isolated container across a bounded batch; untrusted/LLM-synthesized code never weakens the R-SYS6/R-SYS7 guarantees for a speed gain.

2. Domain Model
   Every entity that survives a restart MUST be persisted.

R-DM1 — Project
The top-level configuration aggregate.

Owns:

Root path

Enabled runners

Toolchains

Functions

Workflows

Connectors

Secrets

Module policies

Scheduling configuration

Repair policy (FullyAutonomous vs. RequireApproval)

Projects are the unit of:

Isolation

Trust

Scheduling

Serialization

R-DM2 — Run
Represents test execution.

Lifecycle:

Pending
Running
Passed
Failed
Cancelled
Timed Out
Stalled
Contains:

Machine assignment

Heartbeat

Suite results

Findings

Logs

R-DM3 — Finding
Represents any discovered issue (test failure, security vulnerability, dependency issue, policy violation, code quality problem, or configuration fault).

Contains:

Origin & file location

Root cause signature

Severity & classification

Repair Context (Fixability, complexity, fix status, and fix history when promoted to a repair candidate)

Failure Class (Environment vs. Code — mirrors R-BUILD4; see R-FIX8)

Provenance (originating Run or Workflow Run ID, and back-references to every Repair Attempt made against this Finding — see R-DM3a)

Findings are the unified inputs to the repair pipeline.

R-DM3a — Repair Attempt
The durable record of one pass through the repair pipeline. Contains:

Source Finding(s) — many-to-many (a bundled multi-file patch may resolve several Findings; a Finding may accrue several attempts)

Snapshot reference

Generated patch (diff)

Verify result (build + self-test)

Approval decision & approver identity (if RequireApproval)

Resulting Artifact/commit reference and signed provenance record (R-SEC3)

Repair Attempts make the chain Artifact/commit → Repair Attempt → Finding(s) → originating Run fully queryable ("show me why this line changed"), which is the primary trust surface for FullyAutonomous mode.

R-DM4 — Function
A reusable operation contract exposed by Modules.

Examples:

Run

Build

Git

Zip

Upload

Conversion

Enhancement

Deploy

Connector tasks

Persona tasks

Module tasks

Functions are the atoms of workflows.

R-DM5 — Workflow
An ordered graph of Function executions.

Supports:

Linear execution

Graph execution

Variables

Typed artifacts

Approval gates

Failure policies

Workflow Runs are durable across restarts.

Workflow Run Lifecycle (analogous to R-DM2's Run states):

Pending
Running
AwaitingApproval
PartiallyFailed
Passed
Failed
Cancelled
TimedOut

Aggregate status of a graph Workflow Run is derived from the configured Failure Policy (R-WF3): FailFast escalates any branch failure to Failed; ContinueOnError yields PartiallyFailed when at least one branch fails and at least one succeeds. AwaitingApproval (R-WF5) is a distinct state from Stalled/TimedOut.

Workflow Run Definition Pinning: a Workflow Run captures an immutable snapshot of its Workflow definition — including Function/Module version pins (R-DM7a) — at start time. Edits to the live Workflow definition never affect in-flight Runs; a paused Run always resumes against its captured snapshot, not the current definition. This is the migration story for durable Run state when a Workflow's shape changes underneath it.

R-DM6 — Persona
A configurable role that packages:

System prompt

Prompt template

LLM connection

Execution mode

Additional permissions:

Allowed tools

Allowed connectors

Allowed workflows

Maximum iterations

Maximum LLM cost

Approval policies

Allowed secrets (referenced by handle/token only)

Personas are the single editable home for:

Repair logic

Intent planning

Synthesis

Content generation

Agentic operations

R-DM6a — Design Document as Standing Context
Repair, Intent Planning, and Capability Synthesis Personas (R-DM6) always receive their Project's active Design Document (R-DM16) revisions alongside the source code and Finding/request context — not as an optional tool call, but as standing context injected the same way the system prompt is. This is what distinguishes Telechron's repair/synthesis from source-only autonomous coding: the Persona can check a proposed fix or new capability against stated intent, not only against what currently compiles or passes. Design Document content is untrusted-content-isolated the same as any Finding/connector text (R-LLM5) only insofar as it originates from an external Connector-fed source (e.g. an imported spec); Design Document content edited through R-DM16b's own approval gate is treated as trusted, since it has already passed human review.

R-DM7 — Module
Hot-reloadable physical distribution unit (ZIP / Assembly / Script + Source Code + Self-Test + Permissions).

Contains:

ID

Version

Kind (Script, Assembly, Builtin, Host extension)

Capabilities

Test command

Source code

R-DM7a — Module Versioning & Compatibility
Modules follow semantic versioning. Workflow steps pin a Function to a Module major.minor at authoring time. A hot-reload to a compatible (same-major) version rebinds transparently; a differing major version requires explicit Workflow re-approval before an affected Run resumes. Functions and Connectors (R-DM4, R-DM11) support a deprecation flag so operations can be retired without breaking pinned references. Typed-artifact contracts (R-WF6) may not change within a major version.

R-DM8 — Machine + Resource
Represents:

Agent machines

Hardware capabilities

Managed resources

Examples:

GPUs

Ollama

ComfyUI

TTS engines

Supports mutually exclusive resource groups.

R-DM9 — Intent Plan
A side-effect-free proposal that converts natural language into:

Existing workflows

New workflows

Capability gap analysis

Required modules

Applying a plan is an explicit operation.

R-DM10 — LLM Connection
Named LLM configuration.

Examples:

Ollama

Gemini

Claude

OpenAI

Copilot CLI

OpenCode

Personas reference connections by ID.

R-DM11 — Connector
A configured instance of a Connector Module bound to specific authentication credentials/secrets.

Examples:

GitHub

Discord

SSH

FTP

S3

Dropbox

Google Drive

Jira

Notion

Home Assistant

Web APIs

Connectors are reusable across projects.

R-DM12 — Secret
Represents encrypted project-owned configuration.

Examples:

API keys

SSH keys

Access tokens

Credentials

Secrets may only be referenced by:

Connectors

Functions

Modules

Workflows

Personas

LLM Connections

R-DM13 — Artifact
Represents typed workflow outputs.

Examples:

Markdown

Source code

Images

Audio

JSON

Reports

Zip files

Build outputs

Pull request metadata

Artifacts are passed between workflow steps.

R-DM14 — Toolchain
Defines how a project is:

Built

Tested

Verified

Exported

Deployed

Examples:

Godot

Unity

Unreal

.NET

Python

NodeJS

Rust

Toolchains are provided through modules.

R-DM15 — User & Role
The human identity behind every approval and notification. Every "Human Approval" (R-FIX3, R-BUILD5), "Manual approval" (R-WF5), and "Notify User" (§8) resolves to a User.

Contains:

ID

Authentication credential (see R-SEC8)

Role — at minimum Viewer / Operator (may approve) / Admin

Notification target(s)

Project membership / ownership (Projects are the unit of Trust per R-DM1; ownership is held by Users)

v1 scoping note: a single-operator deployment is permitted, but identity, role, and approver attribution are modeled from the start so multi-user governance needs no domain-model rework. Approval events record which User approved; approval authority is enforced Host-side (R-SEC8), never assumed from mere frontend access.

R-DM16 — Design Document
A Project's living requirements and architectural-intent record — the same role `TechDesign.md` plays for Telechron itself (R-DM16a makes this reflexive). It is the primary context every repair, synthesis, and planning Persona receives, so decisions are checked against what the software is *supposed* to do, not only what it currently does or what its tests currently assert.

Contains:

A set of Requirement entries, each with:

Stable Requirement ID (e.g. `R-DM16`, matching the `R-XXX` convention this document uses)

Title & body text (the intent/architecture prose)

Status (Active / Superseded / Deprecated)

Revision history (who/what changed it, when, and why — see R-DM16b)

Project FK (a Design Document belongs to exactly one Project; Telechron's own `TechDesign.md`/`ImplementationPlan.md` are the Design Document for Telechron's self-repair scope per R-DM16a)

Design Documents are versioned, not overwritten: each edit creates a new revision, preserving the prior text so drift and intent can be diffed over time, mirroring the immutability the rest of the repair-lineage chain relies on (R-DM3a).

R-DM16a — Reflexive Self-Application
Telechron applies R-DM16 to itself: the Host Sentinel's self-repair loop (R-REL3) consults Telechron's own Design Document (seeded from `TechDesign.md` + `ImplementationPlan.md`) exactly as any managed Project's repair Persona consults its Project's Design Document. One mechanism, no special-cased self-repair path — consistent with R-ENG4 (repair primitives are never duplicated).

R-DM16b — Design Document Change Control
Design Document edits are a privileged-path change (R-SEC4): they route to RequireApproval regardless of Project Repair Policy, and are never performed by an autonomous Persona rewriting the document to match code it just produced. This is the control that prevents "drift laundering" — an agent silently updating the spec to agree with whatever it already built, which would erase the entire point of holding intent separate from implementation. A Design Document revision proposed by Capability Synthesis (R-BUILD3) or Repair (R-FIX2) is itself a diff a human reviews and approves before it becomes the new Active revision; humans may also edit it directly outside the pipeline.

3. Core Capabilities
   3.1 Test & Run Execution
   R-RUN1
   The Host dispatches Runs to eligible authenticated Machines.

R-RUN2
Test runners are pluggable and provided through modules.

R-RUN3
Runs emit heartbeats while active.

R-RUN4
Warnings and informational findings do not constitute failures.

R-RUN5 — Toolchain Integration
Runs execute inside containers against a project's configured Toolchain.

Toolchains determine:

Build commands

Test commands

Verification commands

Export commands

Environment requirements

3.2 Findings & Repair
R-FIX1
Runs and workflow failures produce Findings. Findings evaluated as fixable receive Repair Context and enter the repair pipeline. Findings classified as Environment in origin (R-FIX8) are excluded from automatic repair-candidate promotion and route to retry/quarantine instead — only Code-classified Findings receive Repair Context.

R-FIX2
There is exactly one repair pipeline:

Snapshot
→ Generate Fix
→ Apply
→ Verify (in Container)
→ Approval Gate (if required)
→ Revert on Failure
→ Commit / Hot Reload on Success

Generate Fix consults the Project's active Design Document (R-DM16) as standing context (R-DM6a), alongside the Finding and source snapshot — a fix is generated against stated intent, not source code alone.

R-FIX2a — Repair Plan (Batch Aggregation)
For scheduled/batch Finding scans (e.g., the Weekly Security Scan, §8), the pipeline may aggregate multiple related Findings into a single Repair Plan before Generate Fix, so one Approval Gate covers a bundled set of patches rather than one gate per Finding. A Repair Plan is distinct from an Intent Plan (R-DM9, which is NL→workflow/module gap analysis); it is the batch counterpart of a single Repair Attempt (R-DM3a) and shares the same provenance and diff-scope rules (R-SEC3, R-FIX12).

R-FIX3
Repair is bounded, cost-safe, and governed by policy.

Supports:

Attempt caps

Cost caps

Decline short-circuiting

Cross-run deduplication

Project Repair Policy Enforcement: Projects set FullyAutonomous or RequireApproval. In RequireApproval mode, verified patches pause for explicit human confirmation before being committed or hot-reloaded.

R-FIX4
Repair routes by Finding origin.

R-FIX5
Deterministic fixes execute before LLM-based fixes.

R-FIX6
Repair supports bounded rescanning. After a successful patch, the pipeline rescans only the files touched by the patch (not the whole project) for newly introduced Findings, capped at N rescan cycles counted against the same Attempt Cap as R-FIX3 — preventing cascading rescans from spiraling.

R-FIX7
Repairs are atomic multi-file patch transactions.

R-FIX8 — Environment vs. Code Failure Classification
The same distinction R-BUILD4 makes for synthesis applies to all repair-triggering failures. Findings carry a Failure Class (Environment vs. Code). Flaky infra symptoms — Stalled/TimedOut Runs (R-DM2), container network blips, agent heartbeat loss — are Environment and never become repair candidates; feeding them to the pipeline would generate nonsensical "fixes," burn caps, and can produce a patch that "verifies" only because the flake didn't recur. This is the same mechanism as R-BUILD4, not a parallel one (R-NS2/R-ENG4).

R-FIX9 — Repair Concurrency & Locking
Repair targets (files, modules, or projects) are exclusively locked for the duration of a repair pipeline instance; concurrent attempts against overlapping targets are queued, not run in parallel. Repair pipeline execution and module hot-reload draining (R-MOD6) mutually exclude on the same module ID, so a Verify hot-reload can never pull code out from under a second in-flight repair.

R-FIX10 — Repair-Triggered Synthesis Routes Through the Human Gate
If Generate Fix (R-FIX2) determines the repair requires Capability Synthesis (a new module/Function, not a patch to existing code), the pipeline MUST route through R-BUILD5's Human Approval gate regardless of Project Repair Policy. FullyAutonomous governs patch-and-commit of existing code only; it never grants autonomous module installation (see R-NS3). This closes the loophole where repair — a non-NL trigger — could otherwise install capabilities unattended, bypassing R-BUILD5.

R-FIX11 — Oscillation & Regression Detection
Beyond attempt caps, the pipeline keeps a per-file patch-diff signature history. If a newly generated fix's net diff matches (or closely matches) a prior reverted or superseded patch within the same repair lineage, the pipeline short-circuits as an oscillation (fix A reintroduces bug B which reintroduces A…) and escalates to RequireApproval regardless of Project policy, rather than burning the full attempt cap cycling.

R-FIX12 — Repair Diff Scope Limits
Patches exceeding a configurable file-count/line-count threshold, or touching files outside the Finding's declared origin location, require elevated review (distinct UI treatment, mandatory diff-by-diff acknowledgment) regardless of Project Repair Policy. This gives R-SEC4's privileged-path routing and human reviewers a scope signal to work from, countering rubber-stamp fatigue on routine auto-repairs.

R-FIX13 — Architectural Drift Detection
Verify (R-FIX2) includes a Design Document consistency check: the generated patch is evaluated against the Requirement entries (R-DM16) it touches or is tagged against. A patch that satisfies its Finding but contradicts an Active Requirement — implements behavior the Design Document explicitly rules out, or silently narrows/broadens a documented contract — is flagged as a Drift Finding rather than committed/hot-reloaded silently, and routes to RequireApproval regardless of Project Repair Policy (same privileged-path treatment as R-SEC4). This is the mechanical half of R-DM6a's intent-awareness: without it, a Design Document that Personas merely read but nothing ever checks against degrades into unread documentation and architectural drift proceeds exactly as it would without R-DM16 at all.

3.3 Workflows & Functions
R-WF1
Workflow execution is function driven.

R-WF2
Function executors are hot-reloadable.

R-WF3
Failure policies are configurable.

R-WF4
Workflow Runs survive Host restarts.

R-WF5 — Approval Gates
Workflow steps may require:

Automatic execution

Manual approval

Manual edits

Multi-stage approval

Workflow execution pauses until approval requirements are satisfied.

R-WF6 — Typed Artifact Passing
Workflow steps explicitly declare:

Inputs:

Artifact types

Outputs:

Artifact types

3.4 Intent Planning & Capability Synthesis
R-BUILD1
Natural language requests are planned deterministically when a matching rule/pattern exists in the Intent Plan rule set; otherwise planning falls back to Persona-driven (LLM) planning (R-DM6). The Intent Plan (R-DM9) records which path produced it.

R-BUILD2
Intent plans are side-effect free.

R-BUILD3
Capability synthesis generates:

Source code

Self-tests

Modules

All synthesized capabilities are verified inside containers.

Capability Synthesis consults the Project's active Design Document (R-DM16) as standing context (R-DM6a) during generation, and — like Repair (R-FIX13) — Container Verification includes a Design Document consistency check before Module Installation. A synthesized capability that fulfills the Intent Plan but contradicts an Active Requirement is flagged as a Drift Finding at the Human Approval gate (R-BUILD5) rather than installed.

R-BUILD4
Environment failures are distinguished from code failures.

R-BUILD5 — Capability Gap Approval
Natural language requests may never directly synthesize or install modules.

Required flow:

Natural Language Request
↓
Intent Planning
↓
Capability Gap Analysis
↓
Human Approval
↓
Capability Synthesis
↓
Container Verification
↓
Module Installation
↓
Workflow Generation
↓
Execution
3.5 Modules & Extensibility
R-MOD1
Modules support:

Hot reload

Health monitoring

Container execution isolation

R-MOD2
Provider module types:

Test runners

Deployment providers

Function executors

Toolchains

Connectors

LLM engines

R-MOD3
Every module ships:

Source code

Self-tests

R-MOD4
Modules use a unified self-test contract.

R-MOD4a — Self-Test Falsifiability
A passing self-test is only meaningful if it can fail. The Verify stage MUST confirm the self-test fails against the pre-patch/pre-synthesis Snapshot before accepting a post-patch pass — i.e., each synthesized capability ships at least one negative control. This blocks the well-known LLM failure mode (R-BUILD3 has an LLM generate both code and its own test) of a trivially-passing `assert(true)` test, without requiring full coverage analysis.

R-MOD5
Module acquisition is governed by Module Policies.

R-MOD5a — Module Supply-Chain Integrity
Modules acquired via Storefront (R-SYS5) or update MUST be signed by a known publisher key; the Host verifies signature and checksum before installation and refuses load on mismatch. A module's own self-test is evidence of functional health only and is NEVER a security attestation (self-tests run in the same container they're meant to vet, per R-MOD1, and ALC load is not a security boundary, per R-SYS6).

R-MOD5b — Pre-Trust Module Sandboxing
Newly installed or updated modules first run in a maximally restricted container (network-denied, capability-denied beyond self-test needs) with observed behavior compared against declared R-MOD8 capabilities before the Project's granted capabilities take effect for unrestricted execution. Under-declared capability use is a trust failure, not a warning.

R-MOD6
Modules use a two-phase drain protocol.

R-MOD6a — Drain Semantics, Canary & Unload-Leak Guard
Two-phase drain is explicit: phase 1 stops dispatching new invocations to the outgoing version while in-flight Runs/Workflow Runs complete against it or hit a bounded timeout (then are cancelled); phase 2 unloads the old ALC only after confirmed zero references. Post-hot-reload, the module runs a bounded canary/observation window with automatic rollback to the prior version on an elevated error rate (a module can pass its self-test yet fail on real inputs). The Host tracks ALC unload success per cycle and alerts on retained-reference leaks — a known .NET pitfall that, in a system that hot-reloads by design as its core loop, becomes a cumulative Host memory leak if unmonitored.

R-MOD7 — Load Context & Lifecycle Isolation
Modules use isolated AssemblyLoadContext instances for internal type/assembly unloading and hot reloading. Code execution logic and self-tests run strictly inside execution containers (R-SYS6).

R-MOD8 — Capability Permissions
Modules declare required capabilities.

Examples:

Filesystem read

Filesystem write

Internet access

Git access

Process execution

Connector access

GPU access

Secret access

Deployment permissions

LLM access

Projects must approve requested capabilities.

R-MOD8a — Enforced Permission Mediation & Capability Inheritance
All Persona tool/connector/workflow/secret access (R-DM6) and all module capability use (R-MOD8) are mediated by a non-bypassable Host-side authorization check at dispatch time — never by LLM self-restraint. Capabilities newly synthesized by a Persona (R-BUILD3) are capped at the permissions of the synthesizing Persona and must pass the same R-MOD8 approval before becoming callable, so a Persona cannot launder extra privilege into a generated Function/Module.

R-MOD9 — Connector Provider Modules
Modules may provide Connector implementations.

Connector providers declare:

Authentication mechanisms

Required secrets

Supported artifact types

Supported operations

3.6 LLM Providers
R-LLM1
LLM providers are resolved through a registry.

R-LLM2
Connection configuration owns provider settings.

R-LLM3
All LLM calls must track:

Provider

Model

Tokens

Cost

Prompt metadata

and surface them in the UI.

R-LLM4 — Global & Per-Project Spend Caps
Per-repair (R-FIX3) and per-Persona (R-DM6) caps do not bound aggregate spend. The system enforces configurable global and per-project rolling-window LLM spend caps; when exceeded, new repair/synthesis LLM calls are queued or declined (per Repair Policy) until the window resets or an operator overrides. This is the circuit breaker for a Finding burst (e.g., a dependency upgrade breaking many suites at once) launching many individually-capped pipelines in parallel.

R-LLM5 — Untrusted Content Isolation in Prompts
Finding and connector-sourced free-text (CVE descriptions, linter messages, GitHub issue bodies, fetched web content) is attacker-influenceable and MUST be placed in Persona prompts as inert, clearly-delimited data — never as instructions. Personas processing such content operate under a reduced-permission profile that excludes secret access, deployment, and permission/module-modifying tools unless explicitly re-authorized. Prevents prompt injection via a crafted Finding steering an already-privileged repair Persona.

3.7 Machines, Resources & Scheduling
R-SCH1
Runs serialize per machine.

Workflows serialize per project.

R-SCH2
Exclusive resources are mutually exclusive.

R-SCH3
Machine registration requires authentication and deduplication.

R-SCH4
Scheduled executions support:

Testing

Maintenance

Vulnerability scanning

Repair operations

Deployment

Content generation

CI/CD automation

Scheduled executions are Workflow Runs.

Scheduling must be durable and resistant to database failures.

R-SCH5 — Priority, Starvation Prevention & Reconnect
Serialization (R-SCH1) alone does not ensure cross-project fairness. Work Queue dispatch supports priority classes (e.g., interactive repair > scheduled maintenance) with aging to prevent starvation. Agent disconnect during an active Run triggers a bounded grace/reconnect window before the Run is marked Stalled (R-REL1); on reconnect within the window the Run resumes rather than restarting, distinguishing a transient blip from a dead Agent. Agent-pool sizing supports autoscaling hooks driven by queue depth.

3.8 Reliability & Self-Healing
R-REL1
Stalled run watchdog.

R-REL2
SQLite corruption guard.

R-REL3
Host Sentinel repair loop.

The repair engine itself must be hot-reloadable and self-repairable. Self-repair build and test verifications run inside containers. (Self-repair of the repair engine, permission code, or Persona/secret-handling code is a privileged-path change and always routes to RequireApproval per R-SEC4 — it is never FullyAutonomous.)

R-REL4 — Host Scaling Ceiling & Migration Path
The Host is deliberately singleton (R-SYS1): sole DB owner, orchestrator, and realtime hub. This is an accepted v1 tradeoff, not an oversight — but it must be bounded. The Host publishes a documented scaling ceiling (max Agents / concurrent workflows / write throughput) and a stated migration trigger and path (SQLite → networked RDBMS) for when it is exceeded. Agents implement bounded reconnect/backoff and buffer telemetry locally across a Host outage up to a defined window; Host restart-recovery time is bounded and tested.

R-REL5 — Backup, Restore & Recovery
The corruption guard (R-REL2) detects damage but does not recover data. The Host performs scheduled, verified SQLite backups (e.g., VACUUM INTO to a rotating retention set, optionally offsite via Connector) with a documented RPO/RTO. Corruption recovery restores from the most recent verified backup automatically or with operator confirmation — never silently defaulting to an empty database, which would destroy repair-lineage/dedup history (R-FIX11) and cause known-bad fixes to be retried.

R-REL6 — Distributed Tracing & Health Endpoints
Every Run, Workflow Run, and Repair Attempt carries a correlation/trace ID propagated across Host dispatch → Agent execution → container invocation → Host persistence, surfaced in logs and the UI, so a failed repair can be traced across all three trust boundaries. Host and Agents expose liveness/readiness endpoints; the Host Sentinel (R-REL3) and stalled-run watchdog (R-REL1) consume these plus configurable alerting thresholds (queue depth, write latency, repair failure rate) rather than only reactive stall detection.

4. Frontend Requirements
   R-UI1
   Mandatory backend/frontend parity. No feature is considered complete without UI support.

R-UI2
Required surfaces:

Runs

Work Queue

Projects

Workflows (with graph editor)

Machines

Resources

Modules

Connectors

Toolchains

LLM Configurations

Assistant

Storefront

Scheduling

Secrets Management

Design Document (view Requirement entries and revision history; propose/approve edits per R-DM16b; view flagged Drift Findings per R-FIX13)

R-UI3
Realtime updates across all active surfaces.

R-UI4
Feature-first frontend architecture.

5. Persistence
   R-PER1
   SQLite via EF Core is the source of truth.

Persisted entities include:

Projects

Runs

Findings

Workflows

Workflow Runs

Machines

Modules

Toolchains

Connectors

Secrets

Scheduling data

R-PER2
Everything surviving a restart must be persisted.

R-PER3
Persistence uses repository interfaces.

R-PER4
Domain models remain distinct from DB entities.

R-PER5
High-frequency telemetry bypasses direct SQLite writes using batching mechanisms.

R-PER6 — Write-Contention Strategy
SQLite is single-writer. Beyond telemetry (R-PER5), all non-telemetry write paths (Run/Finding/Workflow-Run state transitions, LLM cost records) run in WAL mode with bounded retry/backoff on SQLITE_BUSY, and batch multi-entity atomic transitions into single transactions. The Host exposes a write-latency health signal (feeding R-REL6) so growing contention produces backpressure rather than silent stalls.

R-PER7 — Retention, Archival & Blob Storage
A continuously self-repairing system generates Runs, Findings, logs, and LLM records indefinitely — unbounded growth is the normal operating loop, not an edge case. These are subject to a configurable age/count retention policy with archival to cold storage before deletion; repair-lineage data feeding oscillation/dedup (R-FIX11) is exempted or archived separately to preserve history. Binary Artifacts (R-DM13 — images, audio, zips, build outputs) are stored OUTSIDE SQLite in a filesystem or blob store, with only metadata/references persisted in the DB, so large artifacts never bloat the single-writer file and degrade every other write.

6. Security Requirements
   R-SEC1 — Secret Management & Prompt Safety
   Secrets must be:

Encrypted at rest

Permission gated

Redacted from logs

Scoped per Project

Inaccessible to LLMs in plain text

Raw secrets are never passed inside LLM prompt contexts. Personas operate on opaque tokenized handles; the Host resolves handles to raw secret values strictly at execution time outside the LLM context.

Modules, Functions, Personas, and Connectors must explicitly declare required Secret access.

R-SEC2 — Agent Authentication & Transport Security
Agents must authenticate with the Host using cryptographically signed tokens or mTLS. All commands dispatched to Agents must pass strict schema validation and parameter escaping to prevent command injection.

R-SEC3 — Repair Provenance & Commit Attestation
Every auto-committed or hot-reloaded patch carries a signed, immutable provenance record linking the commit to its source Finding(s), generating Persona, LLM connection/model version, and Verify results (the R-DM3a Repair Attempt is its home). Provenance is stored independently of the artifact it describes. Without this, a poisoned Finding that produces a malicious "fix" under FullyAutonomous leaves no forensic trail — and R-REL3 explicitly lets the repair engine repair itself, so the compromise could be self-reinforcing.

R-SEC4 — Privileged-Path Change Control
Patches touching Persona definitions, permission/capability-evaluation code, the repair pipeline itself, secret-handling code, approval-gate logic, module trust policy, or a Project's Design Document (R-DM16b) MUST route to RequireApproval regardless of Project Repair Policy, and surface on a distinct "privileged diff" review surface. R-FIX7 permits multi-file atomic patches, so without this a single injection could bundle a privilege escalation with a legitimate fix and auto-commit it — patching away the very control that would catch it. Design Document edits are included in this list for the same reason: an autonomous agent that can rewrite its own spec to match whatever it already built can launder architectural drift the same way it could launder a privilege escalation.

R-SEC5 — Secret Resolution Boundary for Agentic Calls
R-SEC1 keeps raw secrets out of prompts, but the Persona still decides which call to make and often constructs the request. Raw secrets are resolved and injected strictly inside the Host/Connector runtime layer at the final hop — never inside the Persona's tool-call construction step — and tool results returned to the LLM are scrubbed/re-tokenized before re-entering prompt context (tool results commonly echo back into the next turn). This closes the largest ambiguity in "never in LLM context."

R-SEC6 — Human-Facing API Authentication & Authorization
R-SEC2 covers only Agent↔Host auth. The REST and Realtime APIs the Frontend uses (which can approve repairs, manage secrets, and install modules) require authenticated User sessions (R-DM15) with per-Project RBAC (Viewer / Operator-Approver / Admin), rate limiting on mutating endpoints, input validation, and CORS restricted to configured origins. An unauthenticated human-facing API is a more direct path to full compromise than the Agent channel.

R-SEC7 — Tamper-Evident Security Audit Log
R-SEC1 only stops secrets leaking INTO logs. Security-relevant actions — secret access, approval decisions (with approver identity), module installs, capability grants, auto-committed repairs — are written to an append-only, hash-chained audit log stored separately from operational telemetry and never subject to the R-PER1 mutation path. An attacker reaching the single SQLite source of truth must not be able to rewrite the record of how a bad patch got in.

R-SEC8 — Secret Lifecycle Management
Secrets (R-DM12) support rotation and immediate revocation; revocation invalidates all outstanding handles and fails in-flight Connector/Function calls using the old value rather than silently continuing. Connectors are reusable across projects (R-DM11), which widens the blast radius of one leaked credential — so a defined revocation path, not manual out-of-band replacement, is required. A Project trust/policy tightening mid-run cancels rather than grandfathers in-flight privileged operations.

R-SEC9 — Key Management for Secrets-at-Rest
"Encrypted at rest" (R-SEC1) is meaningless if the key lives beside the ciphertext. The secret-encryption key is NEVER stored in the same persistence store as the encrypted secrets (SQLite/R-PER1); it comes from a platform key store, HSM, or externally-supplied master key/KEK, and rotates independently of secret rotation. Otherwise filesystem access to the Host yields both key and ciphertext in one grab.

7. Cross-Cutting Engineering Constraints
   R-ENG1
   No source file exceeds approximately 800 lines. Enforced by a lint/CI Function (R-DM4) that emits a code-quality Finding (R-DM3) on violation — advisory, not a hard build failure — so the system can auto-repair its own violations rather than block on them.

R-ENG2
Leaf capabilities belong in modules.

R-ENG3
Shared logic belongs in the SDK.

R-ENG4
Repair primitives are never duplicated.

R-ENG5
Maintain Windows and shell hygiene.

R-ENG6
Every feature ships with tests.

8. Example Workflows
   Godot Auto Repair
   Run Tests (in Container)
   ↓
   Generate Findings
   ↓
   Promote Fixable Findings with Repair Context
   ↓
   Repair Pipeline
   ↓
   Verify Build & Export (in Container)
   ↓
   Check Project Repair Policy Gate (Auto or Approval)
   ↓
   Commit / Hot Reload
   ↓
   Workflow Complete
   Weekly Security Scan
   Scheduled Workflow
   ↓
   Run Security Scanners (in Container)
   ↓
   Generate Findings
   ↓
   Generate Repair Plan (R-FIX2a — aggregates the Findings for one Approval Gate)
   ↓
   Human Approval Gate
   ↓
   Apply Patch & Container Verification
   ↓
   Run Tests
   ↓
   Generate Report Artifact
   ↓
   Create Pull Request via Connector
   ↓
   Notify User
   ↓
   Workflow Complete
   TTRPG Mission Generation
   Generate Mission
   ↓
   Artifact: mission.md
   ↓
   Human Approval Gate
   ↓
   Convert Markdown
   ↓
   Artifact: mission.html
   ↓
   Upload Artifact (Connector: CampaignServer)
   ↓
   Post Discord Announcement (Connector: CampaignDiscord)
   ↓
   Workflow Complete
9. Acceptance Tests
   The restart succeeds when a fresh installation can:

Register an Agent via authenticated tokens/mTLS and execute containerized test suites while streaming realtime telemetry.

Turn failing tests into Findings with Repair Context, and repair them through the unified containerized repair pipeline.

Take a natural language request, perform deterministic intent planning, synthesize missing capabilities after approval, and execute the resulting workflow.

Hot-reload modules using the drain protocol and AssemblyLoadContext isolation.

Execute scheduled workflows for testing, maintenance, deployment, and vulnerability remediation.

Use Toolchains inside containers for project-specific operations such as Godot, Unity, or .NET builds and tests.

Safely interact with external services through Connectors and Secret-backed authentication using prompt handle tokenization.

Pass typed Artifacts between workflow steps.

Enforce module capability permissions and container sandbox restrictions.

Generate a repair or synthesized capability that consults the Project's Design Document as standing context, and correctly flags a Drift Finding — routing to human approval — when the result contradicts an Active Requirement (R-DM16, R-FIX13).

Surface every capability through the frontend.

A feature is not complete until it:

Persists correctly

Is self-testable

Is repairable

Is container safe

Is represented in the UI

Supports the single repair pipeline where applicable

Obeys project trust, security, and capability policies

Emits provenance and tamper-evident audit records for any security-relevant or self-modifying action (R-SEC3, R-SEC7)

Enforces permissions Host-side, never by LLM self-restraint (R-MOD8a), and routes privileged-path changes to human approval (R-SEC4)

Distinguishes environment failures from code failures before triggering repair (R-FIX8)
