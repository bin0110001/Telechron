namespace Telechron.Sdk.Scheduling;

public sealed record QueuedJobItem<T>
{
    public required Guid Id { get; init; }
    public required T Item { get; init; }
    public required int BasePriority { get; init; }
    public required DateTimeOffset QueuedAtUtc { get; init; }
    public double AgedPriority { get; init; }
}

public interface IPriorityQueue<T>
{
    void Enqueue(T item, int priority);
    QueuedJobItem<T>? Dequeue();
    int Count { get; }
    IReadOnlyList<QueuedJobItem<T>> GetSnapshotWithAging();
}
