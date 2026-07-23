namespace Telechron.Sdk.Containers;

// R-SYS6/R-SYS9: everything needed to run one command inside a hard
// isolation boundary. ImageDigest is mandatory and must be a digest
// reference (sha256:...), never a mutable tag — enforced by
// IImageProvenanceVerifier before this ever reaches the container runtime.
public sealed record ContainerExecutionRequest(
    string ImageDigest,
    IReadOnlyList<string> Command,
    string WorkingDirectoryHostPath,
    ContainerResourceLimits ResourceLimits,
    NetworkPolicy NetworkPolicy,
    bool RequiresGpu,
    TimeSpan Timeout);
