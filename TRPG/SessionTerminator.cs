using TRPG.Application.Game;
using TRPG.Application.Worlds.Commands;

namespace TRPG;

internal class SessionTerminator(
    SetWorldPlaytimeCommandHandler setWorldPlaytime,
    GameSessionStateStore sessionStore
)
{
    public async Task EndSession(
        Guid sessionId,
        GameSessionState state,
        CancellationToken cancellationToken = default
    )
    {
        await state.TurnLock.WaitAsync(cancellationToken);
        try
        {
            await setWorldPlaytime.Handle(
                new SetWorldPlaytimeCommand
                {
                    WorldId = state.Session.WorldId,
                    Playtime = GameClock.GetTotalPlaytime(state.Session),
                },
                cancellationToken
            );

            sessionStore.Remove(sessionId);
        }
        finally
        {
            state.TurnLock.Release();
        }
    }
}
