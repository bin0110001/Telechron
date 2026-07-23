using Microsoft.Extensions.Options;
using Telechron.Host.Modules.Health;

namespace Telechron.Host.Modules.Tests;

public class ModuleHealthMonitorTests
{
    private static ModuleHealthMonitor CreateMonitor(
        TimeSpan? rollingWindow = null, int minInvocations = 5, double degradedThreshold = 0.2) =>
        new(Options.Create(new ModuleHealthMonitorOptions
        {
            RollingWindow = rollingWindow ?? TimeSpan.FromMinutes(15),
            MinimumInvocationsBeforeEvaluating = minInvocations,
            DegradedFailureRateThreshold = degradedThreshold,
        }));

    [Fact]
    public void GetStatus_NoInvocationsRecorded_IsUnknown()
    {
        var monitor = CreateMonitor();

        var status = monitor.GetStatus("mod");

        Assert.Equal(ModuleHealthState.Unknown, status.State);
    }

    [Fact]
    public void GetStatus_FewerThanMinimumInvocations_IsHealthyRegardlessOfFailures()
    {
        var monitor = CreateMonitor(minInvocations: 10);

        monitor.RecordInvocationOutcome("mod", succeeded: false);
        monitor.RecordInvocationOutcome("mod", succeeded: false);

        var status = monitor.GetStatus("mod");

        Assert.Equal(ModuleHealthState.Healthy, status.State);
    }

    [Fact]
    public void GetStatus_FailureRateAboveThreshold_IsDegraded()
    {
        var monitor = CreateMonitor(minInvocations: 5, degradedThreshold: 0.2);

        for (var i = 0; i < 8; i++)
            monitor.RecordInvocationOutcome("mod", succeeded: true);
        for (var i = 0; i < 2; i++)
            monitor.RecordInvocationOutcome("mod", succeeded: false); // 20% of 10 -- at threshold
        monitor.RecordInvocationOutcome("mod", succeeded: false); // pushes over 20%

        var status = monitor.GetStatus("mod");

        Assert.Equal(ModuleHealthState.Degraded, status.State);
    }

    [Fact]
    public void GetStatus_FailureRateAtOrBelowThreshold_IsHealthy()
    {
        var monitor = CreateMonitor(minInvocations: 5, degradedThreshold: 0.5);

        for (var i = 0; i < 5; i++)
            monitor.RecordInvocationOutcome("mod", succeeded: true);
        for (var i = 0; i < 5; i++)
            monitor.RecordInvocationOutcome("mod", succeeded: false); // exactly 50%

        var status = monitor.GetStatus("mod");

        Assert.Equal(ModuleHealthState.Healthy, status.State);
    }

    [Fact]
    public void GetStatus_OldInvocationsOutsideRollingWindow_AreExcluded()
    {
        var monitor = CreateMonitor(rollingWindow: TimeSpan.FromMilliseconds(50), minInvocations: 1, degradedThreshold: 0.1);

        monitor.RecordInvocationOutcome("mod", succeeded: false);
        Thread.Sleep(100);

        var status = monitor.GetStatus("mod");

        // The failure has aged out -- back to Unknown, not stuck Degraded forever.
        Assert.Equal(ModuleHealthState.Unknown, status.State);
        Assert.Equal(0, status.TotalInvocations);
    }

    [Fact]
    public void Reset_ClearsRecordedHistory()
    {
        var monitor = CreateMonitor();
        monitor.RecordInvocationOutcome("mod", succeeded: false);

        monitor.Reset("mod");

        Assert.Equal(ModuleHealthState.Unknown, monitor.GetStatus("mod").State);
    }

    [Fact]
    public void GetStatus_TracksTotalAndFailedCounts()
    {
        var monitor = CreateMonitor(minInvocations: 1);
        monitor.RecordInvocationOutcome("mod", succeeded: true);
        monitor.RecordInvocationOutcome("mod", succeeded: true);
        monitor.RecordInvocationOutcome("mod", succeeded: false);

        var status = monitor.GetStatus("mod");

        Assert.Equal(3, status.TotalInvocations);
        Assert.Equal(1, status.FailedInvocations);
        Assert.NotNull(status.LastInvocationAtUtc);
    }
}
