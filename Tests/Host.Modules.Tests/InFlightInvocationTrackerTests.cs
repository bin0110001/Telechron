using Telechron.Host.Modules.Runtime;

namespace Telechron.Host.Modules.Tests;

public class InFlightInvocationTrackerTests
{
    [Fact]
    public void TryBeginInvocation_BeforeDrainStarts_Succeeds()
    {
        var tracker = new InFlightInvocationTracker();

        Assert.True(tracker.TryBeginInvocation("mod"));
        Assert.Equal(1, tracker.GetInFlightCount("mod"));
    }

    [Fact]
    public void TryBeginInvocation_AfterDrainStarts_IsRefused()
    {
        var tracker = new InFlightInvocationTracker();
        tracker.StopAcceptingNewDispatch("mod");

        Assert.False(tracker.TryBeginInvocation("mod"));
    }

    [Fact]
    public void EndInvocation_DecrementsCount()
    {
        var tracker = new InFlightInvocationTracker();
        tracker.TryBeginInvocation("mod");
        tracker.TryBeginInvocation("mod");

        tracker.EndInvocation("mod");

        Assert.Equal(1, tracker.GetInFlightCount("mod"));
    }

    [Fact]
    public void EndInvocation_NeverGoesNegative()
    {
        var tracker = new InFlightInvocationTracker();

        tracker.EndInvocation("mod"); // no matching Begin

        Assert.Equal(0, tracker.GetInFlightCount("mod"));
    }

    [Fact]
    public void StopAcceptingNewDispatch_DoesNotAffectAlreadyInFlightInvocations()
    {
        var tracker = new InFlightInvocationTracker();
        tracker.TryBeginInvocation("mod");

        tracker.StopAcceptingNewDispatch("mod");

        Assert.Equal(1, tracker.GetInFlightCount("mod"));
        Assert.False(tracker.TryBeginInvocation("mod"));
    }
}
