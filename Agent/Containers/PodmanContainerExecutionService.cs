using System.Diagnostics;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;
using Telechron.Sdk.Containers;

namespace Telechron.Agent.Containers;

// R-SYS6/R-SYS7/R-SYS9: the one execution boundary. Every request is
// provenance-checked (digest + allowlisted registry) before Podman ever
// sees it; every container gets CPU/memory quotas and default-deny network
// (NetworkMode "none" unless NetworkPolicy.AllowNetwork is set, and even
// then only the declared egress hosts would be reachable at the network-
// policy layer external to this process — Podman's own network isolation
// is what actually keeps the container off the Host management plane and
// sibling Agents, see containers/README.md).
public sealed class PodmanContainerExecutionService(
    IDockerClient dockerClient,
    IImageProvenanceVerifier provenanceVerifier,
    ILogger<PodmanContainerExecutionService> logger) : IContainerExecutionService
{
    public async Task<ContainerExecutionResult> ExecuteAsync(ContainerExecutionRequest request, CancellationToken ct = default)
    {
        var provenance = provenanceVerifier.Verify(request.ImageDigest);
        if (!provenance.IsAllowed)
        {
            logger.LogWarning("Refused to execute container: {Reason}", provenance.Reason);
            return new ContainerExecutionResult(ContainerExecutionOutcome.Failed, null, string.Empty, provenance.Reason, TimeSpan.Zero);
        }

        var stopwatch = Stopwatch.StartNew();
        string? containerId = null;
        try
        {
            containerId = await CreateContainerAsync(request, ct);
            await dockerClient.Containers.StartContainerAsync(containerId, new ContainerStartParameters(), ct);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(request.Timeout);

            ContainerWaitResponse waitResult;
            try
            {
                waitResult = await dockerClient.Containers.WaitContainerAsync(containerId, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                await TryKillAsync(containerId, ct);
                var (timeoutStdOut, timeoutStdErr) = await ReadLogsAsync(containerId, ct);
                return new ContainerExecutionResult(ContainerExecutionOutcome.TimedOut, null, timeoutStdOut, timeoutStdErr, stopwatch.Elapsed);
            }

            var (stdOut, stdErr) = await ReadLogsAsync(containerId, ct);
            var inspection = await dockerClient.Containers.InspectContainerAsync(containerId, ct);

            // OOMKilled surfaces the R-SYS7 memory-quota enforcement actually
            // firing, distinct from an ordinary non-zero exit.
            var outcome = inspection.State.OOMKilled
                ? ContainerExecutionOutcome.ResourceLimitExceeded
                : ContainerExecutionOutcome.Completed;

            return new ContainerExecutionResult(outcome, (int)waitResult.StatusCode, stdOut, stdErr, stopwatch.Elapsed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Container execution failed for image {ImageDigest}.", request.ImageDigest);
            return new ContainerExecutionResult(ContainerExecutionOutcome.Failed, null, string.Empty, ex.Message, stopwatch.Elapsed);
        }
        finally
        {
            if (containerId is not null)
                await TryRemoveAsync(containerId, ct);
        }
    }

    private async Task<string> CreateContainerAsync(ContainerExecutionRequest request, CancellationToken ct)
    {
        var createParameters = new CreateContainerParameters
        {
            Image = request.ImageDigest,
            Cmd = [.. request.Command],
            WorkingDir = "/workspace",
            HostConfig = new HostConfig
            {
                // R-SYS7: hard resource quotas — no untrusted container runs unbounded.
                Memory = request.ResourceLimits.MemoryBytes,
                MemorySwap = request.ResourceLimits.MemoryBytes, // disable swap-based limit evasion
                NanoCPUs = (long)(request.ResourceLimits.CpuCores * 1_000_000_000),
                StorageOpt = request.ResourceLimits.DiskBytes > 0
                    ? new Dictionary<string, string> { ["size"] = $"{request.ResourceLimits.DiskBytes}" }
                    : null,
                // R-SYS7: default-deny egress; "none" fully isolates the
                // container's network namespace (it cannot reach the Host
                // management plane or sibling Agents, since it has no route
                // to anything). A future allowlisted-egress mode (per
                // NetworkPolicy.AllowedEgressHosts) is a Phase 6 Connector
                // concern layered on top, not a relaxation of this default.
                NetworkMode = request.NetworkPolicy.AllowNetwork ? "bridge" : "none",
                Binds = [$"{request.WorkingDirectoryHostPath}:/workspace"],
                ReadonlyRootfs = false,
                AutoRemove = false, // we remove explicitly after log capture
            },
            Tty = false,
            AttachStdout = true,
            AttachStderr = true,
        };

        var response = await dockerClient.Containers.CreateContainerAsync(createParameters, ct);
        return response.ID;
    }

    private async Task<(string StdOut, string StdErr)> ReadLogsAsync(string containerId, CancellationToken ct)
    {
        var logParameters = new ContainerLogsParameters { ShowStdout = true, ShowStderr = true };
        using var stream = await dockerClient.Containers.GetContainerLogsAsync(containerId, false, logParameters, ct);
        var (stdOut, stdErr) = await stream.ReadOutputToEndAsync(ct);
        return (stdOut, stdErr);
    }

    private async Task TryKillAsync(string containerId, CancellationToken ct)
    {
        try
        {
            await dockerClient.Containers.KillContainerAsync(containerId, new ContainerKillParameters(), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to kill timed-out container {ContainerId}.", containerId);
        }
    }

    private async Task TryRemoveAsync(string containerId, CancellationToken ct)
    {
        try
        {
            await dockerClient.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters { Force = true }, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to remove container {ContainerId}.", containerId);
        }
    }
}
