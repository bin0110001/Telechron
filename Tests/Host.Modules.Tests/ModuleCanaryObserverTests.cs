using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Telechron.Host.Modules.Runtime;

namespace Telechron.Host.Modules.Tests;

public class ModuleCanaryObserverTests
{
    private static ModuleCanaryObserver CreateObserver(TimeSpan windowDuration, int minInvocations = 5, double maxFailureRate = 0.5) =>
        new(Options.Create(new ModuleCanaryOptions
        {
            WindowDuration = windowDuration,
            MinimumInvocationsBeforeEvaluating = minInvocations,
            MaxFailureRate = maxFailureRate,
        }), NullLogger<ModuleCanaryObserver>.Instance);

    [Fact]
    public async Task EvaluateAsync_NoInvocationsRecorded_IsHealthyAfterWindow()
    {
        var observer = CreateObserver(TimeSpan.FromMilliseconds(100));
        observer.StartWindow("mod");

        var result = await observer.EvaluateAsync("mod");

        Assert.Equal(CanaryOutcome.Healthy, result.Outcome);
        Assert.Equal(0, result.TotalInvocations);
    }

    [Fact]
    public async Task EvaluateAsync_AllInvocationsSucceed_IsHealthy()
    {
        var observer = CreateObserver(TimeSpan.FromMilliseconds(150), minInvocations: 3);
        observer.StartWindow("mod");

        for (var i = 0; i < 10; i++)
            observer.RecordInvocationOutcome("mod", succeeded: true);

        var result = await observer.EvaluateAsync("mod");

        Assert.Equal(CanaryOutcome.Healthy, result.Outcome);
        Assert.Equal(10, result.TotalInvocations);
        Assert.Equal(0, result.FailedInvocations);
    }

    [Fact]
    public async Task EvaluateAsync_ElevatedFailureRateAboveMinimum_RollsBackBeforeWindowEnds()
    {
        // Long window (5s) but the failure rate crosses the threshold
        // almost immediately -- EvaluateAsync must return early, not wait
        // out the full window once the verdict is already clear.
        var observer = CreateObserver(TimeSpan.FromSeconds(5), minInvocations: 3, maxFailureRate: 0.5);
        observer.StartWindow("mod");

        for (var i = 0; i < 3; i++)
            observer.RecordInvocationOutcome("mod", succeeded: false);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await observer.EvaluateAsync("mod");
        sw.Stop();

        Assert.Equal(CanaryOutcome.RolledBack, result.Outcome);
        Assert.Equal(3, result.FailedInvocations);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2), $"Expected early rollback, took {sw.Elapsed}.");
    }

    [Fact]
    public async Task EvaluateAsync_FewFailuresBelowMinimumInvocations_DoesNotRollBack()
    {
        var observer = CreateObserver(TimeSpan.FromMilliseconds(150), minInvocations: 10);
        observer.StartWindow("mod");

        // 1 failure out of 1 invocation is a 100% failure rate, but far
        // below the minimum sample size -- must not roll back on noise.
        observer.RecordInvocationOutcome("mod", succeeded: false);

        var result = await observer.EvaluateAsync("mod");

        Assert.Equal(CanaryOutcome.Healthy, result.Outcome);
    }

    [Fact]
    public async Task EvaluateAsync_FailureRateExactlyAtThreshold_DoesNotRollBack()
    {
        var observer = CreateObserver(TimeSpan.FromMilliseconds(150), minInvocations: 4, maxFailureRate: 0.5);
        observer.StartWindow("mod");

        observer.RecordInvocationOutcome("mod", succeeded: false);
        observer.RecordInvocationOutcome("mod", succeeded: false);
        observer.RecordInvocationOutcome("mod", succeeded: true);
        observer.RecordInvocationOutcome("mod", succeeded: true);

        var result = await observer.EvaluateAsync("mod");

        Assert.Equal(CanaryOutcome.Healthy, result.Outcome);
    }

    [Fact]
    public async Task EvaluateAsync_WithoutStartWindow_Throws()
    {
        var observer = CreateObserver(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAsync<InvalidOperationException>(() => observer.EvaluateAsync("never-started"));
    }

    [Fact]
    public void RecordInvocationOutcome_NoActiveWindow_IsIgnoredSilently()
    {
        var observer = CreateObserver(TimeSpan.FromMilliseconds(100));

        // No StartWindow call -- must not throw.
        observer.RecordInvocationOutcome("mod", succeeded: false);
    }
}
