using Microsoft.Extensions.Logging.Abstractions;
using Telechron.Host.Modules.Runtime;

namespace Telechron.Host.Modules.Tests;

public class ModuleDrainCoordinatorTests
{
    [Fact]
    public async Task StartDrainAsync_NoInFlightWork_DrainsImmediately()
    {
        var tracker = new InFlightInvocationTracker();
        var coordinator = new ModuleDrainCoordinator(tracker, NullLogger<ModuleDrainCoordinator>.Instance);

        var result = await coordinator.StartDrainAsync("mod", TimeSpan.FromSeconds(5));

        Assert.Equal(ModuleDrainOutcome.DrainedCleanly, result.Outcome);
    }

    [Fact]
    public async Task StartDrainAsync_InFlightWorkCompletesBeforeTimeout_DrainsCleanly()
    {
        var tracker = new InFlightInvocationTracker();
        tracker.TryBeginInvocation("mod");
        var coordinator = new ModuleDrainCoordinator(tracker, NullLogger<ModuleDrainCoordinator>.Instance);

        var drainTask = coordinator.StartDrainAsync("mod", TimeSpan.FromSeconds(5));
        await Task.Delay(100);
        tracker.EndInvocation("mod");

        var result = await drainTask;

        Assert.Equal(ModuleDrainOutcome.DrainedCleanly, result.Outcome);
    }

    [Fact]
    public async Task StartDrainAsync_InFlightWorkNeverCompletes_TimesOutAndReportsStillInFlight()
    {
        var tracker = new InFlightInvocationTracker();
        tracker.TryBeginInvocation("mod");
        var coordinator = new ModuleDrainCoordinator(tracker, NullLogger<ModuleDrainCoordinator>.Instance);

        var result = await coordinator.StartDrainAsync("mod", TimeSpan.FromMilliseconds(200));

        Assert.Equal(ModuleDrainOutcome.DrainTimedOutAndCancelled, result.Outcome);
        Assert.Equal(1, result.InFlightAtTimeout);
    }

    [Fact]
    public async Task StartDrainAsync_StopsAcceptingNewDispatchImmediately()
    {
        var tracker = new InFlightInvocationTracker();
        var coordinator = new ModuleDrainCoordinator(tracker, NullLogger<ModuleDrainCoordinator>.Instance);

        _ = coordinator.StartDrainAsync("mod", TimeSpan.FromSeconds(5));

        Assert.False(tracker.TryBeginInvocation("mod"));
    }
}
