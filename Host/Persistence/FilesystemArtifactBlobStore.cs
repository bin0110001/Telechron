using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence;

// R-PER7: filesystem-backed Artifact blob storage, keeping large binary
// payloads out of the single-writer SQLite file. BlobRef is a relative path
// under RootDirectory; a random GUID prefix avoids filename collisions while
// a two-level subdirectory split (first 2 + next 2 hex chars) avoids dumping
// millions of files into one directory as Artifacts accumulate over the
// system's continuous self-repair operation.
public sealed class FilesystemArtifactBlobStore(string rootDirectory) : IArtifactBlobStore
{
    public async Task<string> SaveAsync(Stream content, string suggestedFileName, CancellationToken ct = default)
    {
        var id = Guid.NewGuid().ToString("N");
        var relativeDir = Path.Combine(id[..2], id[2..4]);
        var fileName = $"{id}-{SanitizeFileName(suggestedFileName)}";
        var relativePath = Path.Combine(relativeDir, fileName);

        var absoluteDir = Path.Combine(rootDirectory, relativeDir);
        Directory.CreateDirectory(absoluteDir);

        var absolutePath = Path.Combine(rootDirectory, relativePath);
        await using (var fileStream = File.Create(absolutePath))
        {
            await content.CopyToAsync(fileStream, ct);
        }

        return relativePath.Replace(Path.DirectorySeparatorChar, '/');
    }

    public Task<Stream> OpenReadAsync(string blobRef, CancellationToken ct = default)
    {
        var absolutePath = ResolveAbsolutePath(blobRef);
        if (!File.Exists(absolutePath))
            throw new FileNotFoundException("Artifact blob not found.", absolutePath);

        Stream stream = File.OpenRead(absolutePath);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string blobRef, CancellationToken ct = default)
    {
        var absolutePath = ResolveAbsolutePath(blobRef);
        File.Delete(absolutePath);
        return Task.CompletedTask;
    }

    private string ResolveAbsolutePath(string blobRef)
    {
        var normalized = blobRef.Replace('/', Path.DirectorySeparatorChar);
        var absolutePath = Path.GetFullPath(Path.Combine(rootDirectory, normalized));

        // Refuse to resolve a blobRef that escapes rootDirectory (e.g. via
        // "../"), since blobRef values can originate from stored DB rows.
        var rootFullPath = Path.GetFullPath(rootDirectory) + Path.DirectorySeparatorChar;
        if (!absolutePath.StartsWith(rootFullPath, StringComparison.Ordinal))
            throw new InvalidOperationException($"Blob reference '{blobRef}' resolves outside the artifact store root.");

        return absolutePath;
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
        return sanitized.Length == 0 ? "artifact" : sanitized;
    }
}
