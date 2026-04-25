using System.Collections.Concurrent;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Internal;

public sealed class QueueRegistry
{
    private readonly ConcurrentDictionary<string, Queue> _registry = new();
    private readonly Queue _internalQueue = new(Constants.DbosInternalQueue);

    public void Register(Queue queue)
    {
        ArgumentNullException.ThrowIfNull(queue);

        if (queue.Name == Constants.DbosInternalQueue)
            throw new ArgumentException($"{Constants.DbosInternalQueue} is a reserved queue name.");

        if (queue.WorkerConcurrency is not null && queue.Concurrency is not null
            && queue.WorkerConcurrency > queue.Concurrency)
        {
            throw new ArgumentException(
                $"workerConcurrency must be less than or equal to concurrency for queue {queue.Name}.");
        }

        // Matches Java behaviour: duplicate registration is a no-op warning, not an error.
        _registry.TryAdd(queue.Name, queue);
    }

    public Queue? Get(string queueName)
    {
        if (queueName == Constants.DbosInternalQueue) return _internalQueue;
        _registry.TryGetValue(queueName, out var q);
        return q;
    }

    public void Clear() => _registry.Clear();

    public IReadOnlyList<Queue> GetSnapshot()
    {
        var list = new List<Queue>(_registry.Values) { _internalQueue };
        return list.AsReadOnly();
    }
}
