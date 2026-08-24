using System.Collections.Concurrent;

namespace TRPG.GameSessions.Hubs;

internal sealed class PendingEventAckRegistry
{
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource> _pending = new();

    public Task Register(Guid flushId)
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[flushId] = source;
        return source.Task;
    }

    public void Acknowledge(Guid flushId)
    {
        if (_pending.TryRemove(flushId, out var source))
        {
            source.TrySetResult();
        }
    }

    public void Cancel(Guid flushId) => _pending.TryRemove(flushId, out _);
}
