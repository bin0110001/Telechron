using Telechron.Sdk.Grpc;

namespace Telechron.Agent.Dispatch;

// Reverse of ArtifactFetcher -- pushes a locally-produced file (e.g. an
// assembly a container just built for R-BUILD5 capability synthesis) into
// the Host's IArtifactBlobStore over the StoreArtifact client-streaming RPC.
public sealed class ArtifactUploader
{
    private const int ChunkSize = 64 * 1024;

    public async Task<string> UploadFromFileAsync(
        AgentService.AgentServiceClient client, string machineId, string sessionToken, string filePath, string suggestedFileName,
        CancellationToken ct = default)
    {
        using var call = client.StoreArtifact(cancellationToken: ct);

        await using var fileStream = File.OpenRead(filePath);
        var buffer = new byte[ChunkSize];
        var isFirstChunk = true;
        int bytesRead;
        while ((bytesRead = await fileStream.ReadAsync(buffer.AsMemory(0, ChunkSize), ct)) > 0)
        {
            await call.RequestStream.WriteAsync(new StoreArtifactChunk
            {
                MachineId = isFirstChunk ? machineId : string.Empty,
                SessionToken = isFirstChunk ? sessionToken : string.Empty,
                SuggestedFileName = isFirstChunk ? suggestedFileName : string.Empty,
                Data = Google.Protobuf.ByteString.CopyFrom(buffer, 0, bytesRead),
            }, ct);
            isFirstChunk = false;
        }

        // An empty file still needs one chunk carrying auth, per the "auth
        // checked on the first chunk" contract StoreArtifact's Host side
        // expects.
        if (isFirstChunk)
        {
            await call.RequestStream.WriteAsync(new StoreArtifactChunk
            {
                MachineId = machineId,
                SessionToken = sessionToken,
                SuggestedFileName = suggestedFileName,
                Data = Google.Protobuf.ByteString.Empty,
            }, ct);
        }

        await call.RequestStream.CompleteAsync();
        var response = await call.ResponseAsync;
        return response.BlobRef;
    }
}
