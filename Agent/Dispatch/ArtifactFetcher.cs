using global::Grpc.Core;
using Telechron.Sdk.Grpc;

namespace Telechron.Agent.Dispatch;

// Client-side helper for the FetchArtifact RPC -- streams a Host-side blob
// (referenced by an opaque BlobRef, e.g. a module assembly for
// RunModuleSelfTest, R-MOD4/R-MOD5b) down to a local temp file so it can
// be handed to IContainerExecutionService as a workspace bind mount.
public sealed class ArtifactFetcher
{
    public async Task<string> FetchToTempFileAsync(
        AgentService.AgentServiceClient client, string machineId, string sessionToken, string blobRef, CancellationToken ct = default)
    {
        var destinationPath = Path.Combine(Path.GetTempPath(), "telechron-artifact-" + Guid.NewGuid().ToString("N"));

        using var call = client.FetchArtifact(new FetchArtifactRequest
        {
            MachineId = machineId,
            SessionToken = sessionToken,
            BlobRef = blobRef,
        }, cancellationToken: ct);

        await using var fileStream = File.Create(destinationPath);
        await foreach (var chunk in call.ResponseStream.ReadAllAsync(ct))
        {
            if (chunk.Data.Length > 0)
                await fileStream.WriteAsync(chunk.Data.Memory, ct);
            if (chunk.IsFinal)
                break;
        }

        return destinationPath;
    }
}
