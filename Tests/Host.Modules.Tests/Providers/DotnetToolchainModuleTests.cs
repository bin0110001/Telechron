using Microsoft.Extensions.Logging.Abstractions;
using Telechron.Host.Modules.Runtime;
using Telechron.Sdk.Modules.Toolchains;

namespace Telechron.Host.Modules.Tests.Providers;

// Loads the real compiled DotnetToolchain module assembly through the
// real ModuleRuntime/ALC -- proves the ALC type-identity fix (Phase 5)
// generalizes to a second Sdk-defined interface (IToolchainModule), not
// just IModule itself.
public class DotnetToolchainModuleTests
{
    private static string ModuleAssemblyPath =>
        Path.Combine(AppContext.BaseDirectory, "Telechron.Modules.DotnetToolchain.dll");

    [Fact]
    public async Task LoadAsync_RealAssembly_IsAccessibleAsIToolchainModule()
    {
        var runtime = new ModuleRuntime(NullLogger<ModuleRuntime>.Instance);
        await runtime.LoadAsync(ModuleAssemblyPath);

        var toolchain = runtime.GetLoadedAs<IToolchainModule>("telechron.toolchain.dotnet");

        Assert.NotNull(toolchain);
        Assert.Equal("dotnet build", toolchain!.BuildCommand);
        Assert.Equal("dotnet test", toolchain.TestCommand);
        Assert.Contains("@sha256:", toolchain.ToolchainImageDigest);
    }

    [Fact]
    public async Task GetLoadedAs_WrongInterface_ReturnsNull()
    {
        var runtime = new ModuleRuntime(NullLogger<ModuleRuntime>.Instance);
        await runtime.LoadAsync(ModuleAssemblyPath);

        var asRunner = runtime.GetLoadedAs<Telechron.Sdk.Modules.Runners.ITestRunnerModule>("telechron.toolchain.dotnet");

        Assert.Null(asRunner);
    }

    [Fact]
    public async Task RunSelfTestAsync_ValidDescriptor_Passes()
    {
        var runtime = new ModuleRuntime(NullLogger<ModuleRuntime>.Instance);
        var loaded = await runtime.LoadAsync(ModuleAssemblyPath);

        var result = await loaded.Instance.RunSelfTestAsync();

        Assert.True(result.Passed, string.Join("; ", result.Errors));
    }
}
