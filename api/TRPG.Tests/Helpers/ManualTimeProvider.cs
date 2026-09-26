namespace TRPG.Tests.Helpers;

internal sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    private readonly Lock _lock = new();
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _utcNow = utcNow;

    public int TimerCount
    {
        get
        {
            lock (_lock)
            {
                return _timers.Count;
            }
        }
    }

    public override DateTimeOffset GetUtcNow()
    {
        lock (_lock)
        {
            return _utcNow;
        }
    }

    public override ITimer CreateTimer(
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period
    )
    {
        var timer = new ManualTimer(this, callback, state);
        lock (_lock)
        {
            _timers.Add(timer);
            timer.Change(dueTime, period);
        }

        return timer;
    }

    public void Advance(TimeSpan duration)
    {
        DateTimeOffset target;
        lock (_lock)
        {
            target = _utcNow + duration;
        }

        while (FireNextDueTimer(target)) { }

        lock (_lock)
        {
            _utcNow = target;
        }
    }

    private bool FireNextDueTimer(DateTimeOffset target)
    {
        ManualTimer? due;
        lock (_lock)
        {
            due = _timers
                .Where(timer => timer.DueAt != null && timer.DueAt <= target)
                .OrderBy(timer => timer.DueAt)
                .FirstOrDefault();
            if (due == null)
            {
                return false;
            }

            _utcNow = due.DueAt!.Value;
            due.Reschedule(_utcNow);
        }

        due.Fire();
        return true;
    }

    private void Remove(ManualTimer timer)
    {
        lock (_lock)
        {
            _timers.Remove(timer);
        }
    }

    private sealed class ManualTimer(
        ManualTimeProvider owner,
        TimerCallback callback,
        object? state
    ) : ITimer
    {
        private TimeSpan _period = Timeout.InfiniteTimeSpan;

        public DateTimeOffset? DueAt { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            _period = period;
            DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : owner._utcNow + dueTime;
            return true;
        }

        public void Reschedule(DateTimeOffset firedAt) =>
            DueAt = _period == Timeout.InfiniteTimeSpan ? null : firedAt + _period;

        public void Fire() => callback(state);

        public void Dispose() => owner.Remove(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
