using TRPG.Domain;

namespace TRPG.Application.WorldSimulation;

internal sealed class DueEntry(TimeSpan interval)
{
    private GameInstant? _lastObserved;
    private GameInstant _nextDue;

    public bool TakeIfDue(GameInstant now)
    {
        var restart = _lastObserved is not { } last || now < last;
        _lastObserved = now;

        if (restart)
        {
            _nextDue = now + interval;
            return false;
        }

        if (now < _nextDue)
        {
            return false;
        }

        _nextDue = now + interval;
        return true;
    }
}
