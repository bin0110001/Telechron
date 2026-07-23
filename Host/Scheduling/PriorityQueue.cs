namespace Telechron.Host.Scheduling;

using Telechron.Sdk.Scheduling;

public sealed class PriorityQueue<T> : IPriorityQueue<T>
{
    private readonly List<QueuedJobItem<T>> _items = [];
    private readonly object _lock = new();

    public void Enqueue(T item, int priority)
    {
        lock (_lock)
        {
            var job = new QueuedJobItem<T>
            {
                Id = Guid.NewGuid(),
                Item = item,
                BasePriority = priority,
                QueuedAtUtc = DateTimeOffset.UtcNow,
                AgedPriority = priority
            };
            _items.Add(job);
        }
    }

    public QueuedJobItem<T>? Dequeue()
    {
        lock (_lock)
        {
            if (_items.Count == 0) return null;

            var now = DateTimeOffset.UtcNow;
            QueuedJobItem<T>? highest = null;
            double maxScore = double.MinValue;

            foreach (var item in _items)
            {
                var ageSeconds = (now - item.QueuedAtUtc).TotalSeconds;
                var score = item.BasePriority + (ageSeconds * 0.1); // Aging factor: +0.1 priority per second queued
                if (score > maxScore)
                {
                    maxScore = score;
                    highest = item with { AgedPriority = score };
                }
            }

            if (highest != null)
            {
                _items.RemoveAll(i => i.Id == highest.Id);
            }

            return highest;
        }
    }

    public int Count
    {
        get
        {
            lock (_lock) return _items.Count;
        }
    }

    public IReadOnlyList<QueuedJobItem<T>> GetSnapshotWithAging()
    {
        lock (_lock)
        {
            var now = DateTimeOffset.UtcNow;
            return _items.Select(item =>
            {
                var ageSeconds = (now - item.QueuedAtUtc).TotalSeconds;
                return item with { AgedPriority = item.BasePriority + (ageSeconds * 0.1) };
            }).ToList();
        }
    }
}
