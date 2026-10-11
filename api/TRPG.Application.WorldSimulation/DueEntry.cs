using TRPG.Domain;

namespace TRPG.Application.WorldSimulation;

internal sealed class DueEntry(TimeSpan interval)
{
    private GameInstant? _lastObserved;
    private GameInstant _nextDue;

    public void Restart(GameInstant now)
    {
        _lastObserved = now;
        _nextDue = now + interval;
    }

    public bool TakeIfDue(GameInstant now)
    {
        if (_lastObserved is not { } last || now < last)
        {
            Restart(now);
            return false;
        }

        _lastObserved = now;

        if (now < _nextDue)
        {
            return false;
        }

        _nextDue = now + interval;
        return true;
    }
}
