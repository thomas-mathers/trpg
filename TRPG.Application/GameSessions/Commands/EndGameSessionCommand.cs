using Microsoft.Extensions.Caching.Memory;
using TRPG.Application.Combat.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.Worlds.Commands;

namespace TRPG.Application.GameSessions.Commands;

internal class EndGameSessionCommand
{
    public required Guid SessionId { get; init; }
}

internal class EndGameSessionCommandHandler(
    SetWorldPlaytimeCommandHandler setWorldPlaytime,
    GetGameSessionQueryHandler getGameSession,
    DeleteGameSessionCommandHandler deleteGameSession,
    AbandonActiveFightCommandHandler abandonActiveFight,
    IMemoryCache cache
)
{
    public async Task Handle(
        EndGameSessionCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var snapshot = await getGameSession.Handle(
            new GetGameSessionQuery { SessionId = command.SessionId },
            cancellationToken
        );

        await setWorldPlaytime.Handle(
            new SetWorldPlaytimeCommand
            {
                WorldId = snapshot.WorldId,
                Playtime = snapshot.Playtime,
            },
            cancellationToken
        );

        await abandonActiveFight.Handle(
            new AbandonActiveFightCommand
            {
                WorldId = snapshot.WorldId,
                PlayerId = snapshot.PlayerId,
                Playtime = snapshot.Playtime,
            },
            cancellationToken
        );

        await deleteGameSession.Handle(
            new DeleteGameSessionCommand { SessionId = command.SessionId },
            cancellationToken
        );

        // The world isn't gone - this just frees the cached entity list/automaton while no
        // session is active against it. The next session that resumes this world rebuilds
        // them lazily on its first turn.
        cache.Remove(GetNamedEntitiesByWorldQueryHandler.CacheKey(snapshot.WorldId));
        cache.Remove(GetEntityNameAutomatonByWorldQueryHandler.CacheKey(snapshot.WorldId));
    }
}
