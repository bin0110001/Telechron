namespace Telechron.Sdk.Persistence;

// R-PER7: Artifact binary payloads live outside SQLite; this is where they
// actually live. BlobRef is opaque to callers — treat it as a handle, not a
// path to construct manually.
public interface IArtifactBlobStore
{
    Task<string> SaveAsync(Stream content, string suggestedFileName, CancellationToken ct = default);
    Task<Stream> OpenReadAsync(string blobRef, CancellationToken ct = default);
    Task DeleteAsync(string blobRef, CancellationToken ct = default);
}
