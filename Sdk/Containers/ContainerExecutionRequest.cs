namespace Telechron.Sdk.Containers;

// R-SYS6/R-SYS9: everything needed to run one command inside a hard
// isolation boundary. ImageDigest is mandatory and must be a digest
// reference (sha256:...), never a mutable tag — enforced by
// IImageProvenanceVerifier before this ever reaches the container runtime.
//
// R-SYS10: WarmPoolKey is opt-in and null by default. Only the caller (the
// repair pipeline, for deterministic non-LLM fixes per R-FIX5) sets it,
// and only for a bounded batch of same-trust-boundary executions.
// Untrusted/LLM-synthesized code MUST leave this null -- every request
// still gets a fresh container from a warm base, per-run isolation is
// never traded away for speed (R-SYS6/R-SYS7 unweakened).
public sealed record ContainerExecutionRequest(
    string ImageDigest,
    IReadOnlyList<string> Command,
    string WorkingDirectoryHostPath,
    ContainerResourceLimits ResourceLimits,
    NetworkPolicy NetworkPolicy,
    bool RequiresGpu,
    TimeSpan Timeout,
    WarmPoolKey? WarmPoolKey = null);
