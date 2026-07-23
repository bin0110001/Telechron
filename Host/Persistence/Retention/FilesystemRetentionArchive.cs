using Telechron.Sdk.Persistence;

namespace Telechron.Host.Persistence.Retention;

// R-PER7: appends archived rows as JSON Lines, one file per (entity type,
// UTC day) so files stay bounded and are trivially inspectable/shippable to
// a real cold-storage target later behind this same interface. Lives
// alongside the Artifact blob store on the filesystem — same out-of-SQLite
// pattern, this is just another kind of blob.
public sealed class FilesystemRetentionArchive(string rootDirectory) : IRetentionArchive
{
    public async Task AppendAsync(string entityTypeName, string rowJson, CancellationToken ct = default)
    {
        var day = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");
        var dir = Path.Combine(rootDirectory, entityTypeName);
        Directory.CreateDirectory(dir);

        var filePath = Path.Combine(dir, $"{day}.jsonl");
        await File.AppendAllTextAsync(filePath, rowJson + Environment.NewLine, ct);
    }
}
