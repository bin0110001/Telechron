# Container Runtime & Image Provenance

**Runtime:** [Podman](https://podman.io/) (daemonless, rootless-by-default), targeting R-SYS6
(container execution isolation boundary) and R-SYS7 (resource/network policy). Rootless execution
gives a stronger default isolation posture than a root daemon for the untrusted/synthesized code
this system runs — a good match for R-SYS7's requirement that untrusted containers can't reach the
Host management plane or sibling Agents.

Podman's CLI is Docker-CLI-compatible; Agent-side container orchestration code should target the
`docker` CLI/API surface where possible so it stays portable to Docker if a deployment needs it.

## Registry Allowlist (R-SYS9)

Only images from these registries may be pulled for execution/verification containers:

- `mcr.microsoft.com` — official Microsoft container images (.NET, etc.)
- `docker.io/library` — Docker Official Images (verified publisher program)

Any additional registry must be added here explicitly and reviewed — this file is the source of
truth the Host's image-provenance check validates pulls against (implemented in Phase 4).

## Pinned Base Images

Images are pinned by **digest**, never by mutable tag. Tags are recorded alongside the digest for
human readability only; the digest is what's actually used at pull/run time.

| Purpose | Image | Tag (informational) | Digest |
|---|---|---|---|
| Base OS / general toolchain | `mcr.microsoft.com/dotnet/sdk` | `9.0-noble` | `sha256:72b2c1fba104eed0765e76c66256dd57b8b00c5e7c7fd16ad3eb254ad18db3fc` |
| .NET execution/runtime-only | `mcr.microsoft.com/dotnet/runtime` | `9.0-noble` | `sha256:156e9fd1351359ac2b8cd3e05676de78bfb1a8937f9b221b50f9ccf6984c7093` |

Pull by digest:

```
podman pull mcr.microsoft.com/dotnet/sdk@sha256:72b2c1fba104eed0765e76c66256dd57b8b00c5e7c7fd16ad3eb254ad18db3fc
```

## CVE Rescanning

Digests above are current as of 2026-07-22. R-SYS9 requires periodic CVE rescanning of pinned
images — this is wired up as a scheduled workflow in Phase 9; until then, re-verify manually before
pinning a new digest.

## Toolchain Image Definitions

Toolchain-specific image build definitions live under `containers/toolchains/<name>/`, version
-controlled alongside the Toolchain module that references them (R-SYS9, R-DM14). The first
end-to-end toolchain is `dotnet` (see `containers/toolchains/dotnet/`); others (Godot, Node,
Python, …) are added as clones once Phase 6 starts.
