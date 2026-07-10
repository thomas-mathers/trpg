using System.Collections.Concurrent;
using TRPG.Application;

namespace TRPG;

internal class GameSessionStateStore
{
    private readonly ConcurrentDictionary<Guid, GameSessionState> _sessions = new();

    public Guid Add(GameSessionState state)
    {
        var sessionId = Guid.NewGuid();
        _sessions[sessionId] = state;
        return sessionId;
    }

    public GameSessionState? Get(Guid sessionId) => _sessions.GetValueOrDefault(sessionId);

    public void Remove(Guid sessionId) => _sessions.TryRemove(sessionId, out _);
}
