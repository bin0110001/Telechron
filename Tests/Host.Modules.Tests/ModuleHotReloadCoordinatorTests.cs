using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Telechron.Host.Modules.Runtime;

namespace Telechron.Host.Modules.Tests;

// End-to-end proof of the Phase 5 module runtime "Done when" bars against
// the real compiled Sample module assembly: a hot-reload with in-flight
// work drains cleanly, a broken new version auto-rolls-back, and repeated
// reload cycles show no unload leak.
public class ModuleHotReloadCoordinatorTests
{
    private static string SampleModuleAssemblyPath =>
        Path.Combine(AppContext.BaseDirectory, "Telechron.Modules.Sample.dll");

    private sealed record Harness(
        ModuleHotReloadCoordinator Coordinator, InFlightInvocationTracker Tracker, IModuleRuntime Runtime, IModuleCanaryObserver Canary);

    private static Harness CreateHarness(TimeSpan canaryWindow, int minInvocations = 1, double maxFailureRate = 0.5)
    {
        var tracker = new InFlightInvocationTracker();
        var runtime = new ModuleRuntime(NullLogger<ModuleRuntime>.Instance);
        var drain = new ModuleDrainCoordinator(tracker, NullLogger<ModuleDrainCoordinator>.Instance);
        var canary = new ModuleCanaryObserver(
            Options.Create(new ModuleCanaryOptions
            {
                WindowDuration = canaryWindow,
                MinimumInvocationsBeforeEvaluating = minInvocations,
                MaxFailureRate = maxFailureRate,
            }), NullLogger<ModuleCanaryObserver>.Instance);
        var coordinator = new ModuleHotReloadCoordinator(drain, runtime, canary, NullLogger<ModuleHotReloadCoordinator>.Instance);
        return new Harness(coordinator, tracker, runtime, canary);
    }

    [Fact]
    public async Task ReloadAsync_HealthyCanary_ReloadsSuccessfully()
    {
        var h = CreateHarness(TimeSpan.FromMilliseconds(150));
        await h.Runtime.LoadAsync(SampleModuleAssemblyPath);

        // Simulate healthy real traffic against the new version during
        // its canary window.
        var trafficTask = Task.Run(async () =>
        {
            for (var i = 0; i < 20; i++)
            {
                h.Canary.RecordInvocationOutcome("telechron.sample", succeeded: true);
                await Task.Delay(10);
            }
        });

        var result = await h.Coordinator.ReloadAsync(
            "telechron.sample", SampleModuleAssemblyPath, SampleModuleAssemblyPath, TimeSpan.FromSeconds(5));
        await trafficTask;

        Assert.Equal(ModuleHotReloadOutcome.ReloadedSuccessfully, result.Outcome);
        Assert.Equal(ModuleDrainOutcome.DrainedCleanly, result.DrainResult.Outcome);
        Assert.False(result.OldVersionUnloadLeakDetected);
        Assert.NotNull(h.Runtime.GetLoaded("telechron.sample"));
    }

    [Fact]
    public async Task ReloadAsync_InFlightWorkCompletesDuringDrain_DrainsCleanlyBeforeUnloading()
    {
        var h = CreateHarness(TimeSpan.FromMilliseconds(100));
        await h.Runtime.LoadAsync(SampleModuleAssemblyPath);
        h.Tracker.TryBeginInvocation("telechron.sample");

        var reloadTask = h.Coordinator.ReloadAsync(
            "telechron.sample", SampleModuleAssemblyPath, SampleModuleAssemblyPath, TimeSpan.FromSeconds(5));
        await Task.Delay(100);
        h.Tracker.EndInvocation("telechron.sample");

        var result = await reloadTask;

        Assert.Equal(ModuleDrainOutcome.DrainedCleanly, result.DrainResult.Outcome);
        Assert.Equal(ModuleHotReloadOutcome.ReloadedSuccessfully, result.Outcome);
    }

    [Fact]
    public async Task ReloadAsync_ElevatedFailureRateDuringCanary_RollsBackToOutgoingVersion()
    {
        var h = CreateHarness(TimeSpan.FromSeconds(5), minInvocations: 3, maxFailureRate: 0.5);
        await h.Runtime.LoadAsync(SampleModuleAssemblyPath);

        // The canary window only starts once ReloadAsync's own load step
        // completes, which races with this traffic generator -- keep
        // recording failures for the whole 5s window (RecordInvocationOutcome
        // is a harmless no-op before the window starts and after it ends)
        // rather than a fixed handful of attempts that could all land too
        // early and be silently dropped.
        using var trafficCts = new CancellationTokenSource();
        var trafficTask = Task.Run(async () =>
        {
            while (!trafficCts.IsCancellationRequested)
            {
                h.Canary.RecordInvocationOutcome("telechron.sample", succeeded: false);
                await Task.Delay(10, trafficCts.Token).ContinueWith(_ => { }, TaskScheduler.Default);
            }
        });

        var result = await h.Coordinator.ReloadAsync(
            "telechron.sample", SampleModuleAssemblyPath, SampleModuleAssemblyPath, TimeSpan.FromSeconds(5));
        await trafficCts.CancelAsync();
        await trafficTask;

        Assert.Equal(ModuleHotReloadOutcome.RolledBackAfterCanaryFailure, result.Outcome);
        Assert.NotNull(result.CanaryResult);
        Assert.Equal(CanaryOutcome.RolledBack, result.CanaryResult!.Outcome);
        // Rolled back to the outgoing assembly -- the module is still
        // loaded and callable, not left in an unloaded/broken state.
        Assert.NotNull(h.Runtime.GetLoaded("telechron.sample"));
    }

    [Fact]
    public async Task ReloadAsync_NewVersionFailsToLoad_RollsBackWithoutRunningCanary()
    {
        var h = CreateHarness(TimeSpan.FromSeconds(5));
        await h.Runtime.LoadAsync(SampleModuleAssemblyPath);

        var nonModuleAssemblyPath = Path.Combine(AppContext.BaseDirectory, "Telechron.Sdk.dll");

        var result = await h.Coordinator.ReloadAsync(
            "telechron.sample", nonModuleAssemblyPath, SampleModuleAssemblyPath, TimeSpan.FromSeconds(5));

        Assert.Equal(ModuleHotReloadOutcome.RolledBackAfterLoadFailure, result.Outcome);
        Assert.Null(result.CanaryResult);
        Assert.NotNull(h.Runtime.GetLoaded("telechron.sample"));
    }

    [Fact]
    public async Task ReloadAsync_RepeatedCyclesWithHealthyCanary_NoUnloadLeakAcrossCycles()
    {
        var h = CreateHarness(TimeSpan.FromMilliseconds(60), minInvocations: 100); // high min -> never evaluates as failing
        await h.Runtime.LoadAsync(SampleModuleAssemblyPath);

        for (var i = 0; i < 3; i++)
        {
            var result = await h.Coordinator.ReloadAsync(
                "telechron.sample", SampleModuleAssemblyPath, SampleModuleAssemblyPath, TimeSpan.FromSeconds(5));

            Assert.False(result.OldVersionUnloadLeakDetected, $"Leak detected on cycle {i}.");
            Assert.Equal(ModuleHotReloadOutcome.ReloadedSuccessfully, result.Outcome);
        }
    }
}
