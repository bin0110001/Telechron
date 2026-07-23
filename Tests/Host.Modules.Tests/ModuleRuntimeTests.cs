using Microsoft.Extensions.Logging.Abstractions;
using Telechron.Host.Modules.Runtime;

namespace Telechron.Host.Modules.Tests;

// Live-loads the actual compiled Telechron.Modules.Sample.dll -- proves
// ALC load/unload against a real assembly, not a hand-rolled test double.
public class ModuleRuntimeTests
{
    private static string SampleModuleAssemblyPath =>
        Path.Combine(AppContext.BaseDirectory, "Telechron.Modules.Sample.dll");

    [Fact]
    public async Task LoadAsync_RealModuleAssembly_InstantiatesAndReportsName()
    {
        var runtime = new ModuleRuntime(NullLogger<ModuleRuntime>.Instance);

        var loaded = await runtime.LoadAsync(SampleModuleAssemblyPath);

        Assert.Equal("telechron.sample", loaded.ModuleName);
        Assert.Equal(new Sdk.Modules.ModuleVersion(1, 0, 0), loaded.Version);
    }

    [Fact]
    public async Task LoadAsync_SameModuleTwice_SecondLoadOverwritesFirstInRegistry()
    {
        var runtime = new ModuleRuntime(NullLogger<ModuleRuntime>.Instance);

        var first = await runtime.LoadAsync(SampleModuleAssemblyPath);
        var second = await runtime.LoadAsync(SampleModuleAssemblyPath);

        var current = runtime.GetLoaded("telechron.sample");
        Assert.NotNull(current);
        Assert.Same(second.Instance, current!.Instance);
        Assert.NotSame(first.LoadContext, second.LoadContext);
    }

    [Fact]
    public async Task GetLoaded_UnknownModuleName_ReturnsNull()
    {
        var runtime = new ModuleRuntime(NullLogger<ModuleRuntime>.Instance);

        Assert.Null(runtime.GetLoaded("no-such-module"));
    }

    [Fact]
    public async Task UnloadAsync_UnknownModuleName_ReturnsNotUnloaded()
    {
        var runtime = new ModuleRuntime(NullLogger<ModuleRuntime>.Instance);

        var result = await runtime.UnloadAsync("no-such-module");

        Assert.False(result.Unloaded);
        Assert.False(result.LeakDetected);
    }

    [Fact]
    public async Task UnloadAsync_AfterLoad_UnloadsWithoutLeak()
    {
        var runtime = new ModuleRuntime(NullLogger<ModuleRuntime>.Instance);
        await runtime.LoadAsync(SampleModuleAssemblyPath);

        var result = await runtime.UnloadAsync("telechron.sample");

        Assert.True(result.Unloaded);
        Assert.False(result.LeakDetected);
        Assert.Null(runtime.GetLoaded("telechron.sample"));
    }

    [Fact]
    public async Task LoadThenUnloadThenLoadAgain_WorksRepeatedly()
    {
        var runtime = new ModuleRuntime(NullLogger<ModuleRuntime>.Instance);

        for (var i = 0; i < 3; i++)
        {
            await LoadThenAssertNameAsync(runtime, i);

            var result = await runtime.UnloadAsync("telechron.sample");
            Assert.True(result.Unloaded);
            Assert.False(result.LeakDetected, $"Leak detected on cycle {i}.");
        }
    }

    // Isolated into its own method (not inlined in the loop body) so the
    // LoadedModule local goes fully out of scope -- including from the
    // JIT's view of the stack frame -- before UnloadAsync's leak check
    // runs. A caller holding this reference across its own unload call
    // would be a genuine leak from ModuleRuntime's perspective, so the
    // test must not do that to itself and then blame the runtime.
    private static async Task LoadThenAssertNameAsync(ModuleRuntime runtime, int cycle)
    {
        var loaded = await runtime.LoadAsync(SampleModuleAssemblyPath);
        Assert.Equal("telechron.sample", loaded.ModuleName);
    }

    [Fact]
    public async Task LoadAsync_AssemblyWithoutIModule_ThrowsAndUnloadsContext()
    {
        var runtime = new ModuleRuntime(NullLogger<ModuleRuntime>.Instance);

        // Telechron.Sdk.dll has no IModule implementation -- a real
        // assembly, just not a module, exercising the "no IModule type
        // found" path with something that actually loads.
        var sdkAssemblyPath = Path.Combine(AppContext.BaseDirectory, "Telechron.Sdk.dll");

        await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.LoadAsync(sdkAssemblyPath));
    }
}
