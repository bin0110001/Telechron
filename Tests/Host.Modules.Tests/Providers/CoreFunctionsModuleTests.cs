using System.IO.Compression;
using Telechron.Modules.CoreFunctions;

namespace Telechron.Host.Modules.Tests.Providers;

public class CoreFunctionsModuleTests : IDisposable
{
    private readonly CoreFunctionsModule _module = new();
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "telechron-corefunctions-" + Guid.NewGuid().ToString("N"));

    public CoreFunctionsModuleTests() => Directory.CreateDirectory(_workDir);

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
            Directory.Delete(_workDir, recursive: true);
    }

    [Fact]
    public async Task RunSelfTestAsync_Passes()
    {
        var result = await _module.RunSelfTestAsync();

        Assert.True(result.Passed, string.Join("; ", result.Errors));
    }

    [Fact]
    public void RequiresContainer_Zip_IsFalse()
    {
        Assert.False(_module.RequiresContainer("zip"));
    }

    [Fact]
    public void RequiresContainer_Git_IsTrue()
    {
        Assert.True(_module.RequiresContainer("git"));
    }

    [Fact]
    public void RequiresContainer_UnknownKind_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _module.RequiresContainer("not-a-real-kind"));
    }

    [Fact]
    public async Task InvokeInProcessAsync_Zip_ActuallyCreatesRealZipFileWithCorrectContents()
    {
        var sourceDir = Path.Combine(_workDir, "source");
        Directory.CreateDirectory(sourceDir);
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "hello.txt"), "hello telechron");
        var destZip = Path.Combine(_workDir, "output.zip");

        var parametersJson = $$"""{"sourceDirectory": "{{sourceDir.Replace("\\", "\\\\")}}", "destinationZipPath": "{{destZip.Replace("\\", "\\\\")}}"}""";

        var result = await _module.InvokeInProcessAsync("zip", "[]", parametersJson);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.True(File.Exists(destZip));

        using var archive = ZipFile.OpenRead(destZip);
        var entry = archive.GetEntry("hello.txt");
        Assert.NotNull(entry);
        using var reader = new StreamReader(entry!.Open());
        Assert.Equal("hello telechron", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task InvokeInProcessAsync_Zip_PathTraversalInSourceDirectory_IsRejected()
    {
        var parametersJson = """{"sourceDirectory": "../../etc", "destinationZipPath": "out.zip"}""";

        var result = await _module.InvokeInProcessAsync("zip", "[]", parametersJson);

        Assert.False(result.Succeeded);
        Assert.Contains("traversal", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvokeInProcessAsync_Zip_PathTraversalInDestination_IsRejected()
    {
        var sourceDir = Path.Combine(_workDir, "source");
        Directory.CreateDirectory(sourceDir);
        var parametersJson = $$"""{"sourceDirectory": "{{sourceDir.Replace("\\", "\\\\")}}", "destinationZipPath": "../../../evil.zip"}""";

        var result = await _module.InvokeInProcessAsync("zip", "[]", parametersJson);

        Assert.False(result.Succeeded);
        Assert.Contains("traversal", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvokeInProcessAsync_Zip_MissingSourceDirectory_FailsGracefully()
    {
        var parametersJson = """{"sourceDirectory": "definitely-does-not-exist-anywhere", "destinationZipPath": "out.zip"}""";

        var result = await _module.InvokeInProcessAsync("zip", "[]", parametersJson);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task InvokeInProcessAsync_ContainerRequiredKind_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _module.InvokeInProcessAsync("git", "[]", """{"gitCommand": "status"}"""));
    }

    [Fact]
    public void BuildContainerCommand_Git_ProducesExpectedArgv()
    {
        var command = _module.BuildContainerCommand("git", "[]", """{"gitCommand": "clone https://example.invalid/repo.git"}""");

        Assert.Equal(["git", "clone", "https://example.invalid/repo.git"], command);
    }

    [Fact]
    public void BuildContainerCommand_InProcessKind_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _module.BuildContainerCommand("zip", "[]", """{"sourceDirectory": "x", "destinationZipPath": "y"}"""));
    }
}
