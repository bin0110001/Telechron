using System.Text;
using Telechron.Host.Persistence;

namespace Telechron.Host.Persistence.Tests.Phase3;

public sealed class FilesystemArtifactBlobStoreTests : IDisposable
{
    private readonly string _root;
    private readonly FilesystemArtifactBlobStore _store;

    public FilesystemArtifactBlobStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "telechron-tests", "blobstore-" + Guid.NewGuid().ToString("N"));
        _store = new FilesystemArtifactBlobStore(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best-effort */ }
    }

    [Fact]
    public async Task SaveThenOpenRead_RoundTripsContent()
    {
        var content = "artifact content"u8.ToArray();
        using var stream = new MemoryStream(content);

        var blobRef = await _store.SaveAsync(stream, "report.json");

        await using var readStream = await _store.OpenReadAsync(blobRef);
        using var reader = new StreamReader(readStream, Encoding.UTF8);
        var readBack = await reader.ReadToEndAsync();

        Assert.Equal("artifact content", readBack);
    }

    [Fact]
    public async Task Save_DoesNotWriteIntoAnySqliteFile()
    {
        // R-PER7: storing a large artifact must not grow the SQLite file —
        // proven here by confirming the blob lands as an independent file on
        // disk under the store root, nothing to do with any .db file.
        var content = new byte[1024 * 1024]; // 1MB
        Random.Shared.NextBytes(content);
        using var stream = new MemoryStream(content);

        var blobRef = await _store.SaveAsync(stream, "large-build-output.zip");

        var files = Directory.GetFiles(_root, "*", SearchOption.AllDirectories);
        Assert.Contains(files, f => f.EndsWith("large-build-output.zip", StringComparison.Ordinal));
        Assert.DoesNotContain(files, f => f.EndsWith(".db", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Save_TwoFilesWithSameSuggestedName_DoNotCollide()
    {
        using var streamA = new MemoryStream("content A"u8.ToArray());
        using var streamB = new MemoryStream("content B"u8.ToArray());

        var refA = await _store.SaveAsync(streamA, "report.json");
        var refB = await _store.SaveAsync(streamB, "report.json");

        Assert.NotEqual(refA, refB);

        await using var readA = await _store.OpenReadAsync(refA);
        await using var readB = await _store.OpenReadAsync(refB);
        using var readerA = new StreamReader(readA);
        using var readerB = new StreamReader(readB);

        Assert.Equal("content A", await readerA.ReadToEndAsync());
        Assert.Equal("content B", await readerB.ReadToEndAsync());
    }

    [Fact]
    public async Task Delete_RemovesBlob()
    {
        using var stream = new MemoryStream("to be deleted"u8.ToArray());
        var blobRef = await _store.SaveAsync(stream, "temp.txt");

        await _store.DeleteAsync(blobRef);

        await Assert.ThrowsAsync<FileNotFoundException>(() => _store.OpenReadAsync(blobRef));
    }

    [Fact]
    public async Task OpenRead_RejectsPathTraversalAttempt()
    {
        // blobRef values can originate from stored DB rows — must not allow
        // escaping the store root via a crafted "../" reference.
        await Assert.ThrowsAsync<InvalidOperationException>(() => _store.OpenReadAsync("../../etc/passwd"));
    }
}
