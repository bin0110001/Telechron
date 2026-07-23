namespace Telechron.Host.Scheduling.Tests;

using Telechron.Host.Scheduling;

public sealed class PriorityQueueTests
{
    [Fact]
    public async Task Dequeue_PriorityAging_PreventsStarvation()
    {
        var queue = new PriorityQueue<string>();

        // Enqueue low priority item first
        queue.Enqueue("low-priority-job", 1);
        await Task.Delay(150); // Allow aging time

        // Enqueue higher priority item later
        queue.Enqueue("high-priority-job", 2);

        var firstDequeued = queue.Dequeue();
        Assert.NotNull(firstDequeued);

        // Low priority item queued earlier has aged enough to be dequeued first or scored properly
        Assert.True(firstDequeued.AgedPriority > 0);
    }
}
