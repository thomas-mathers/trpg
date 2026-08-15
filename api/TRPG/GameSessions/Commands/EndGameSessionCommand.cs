using Microsoft.Extensions.Caching.Memory;
using TRPG.Application.Combat.Commands;
using TRPG.Application.Common.Handling;
using TRPG.Application.GameSessions.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Narration.Queries;
using TRPG.Application.Worlds.Commands;
using TRPG.Data.Models;

namespace TRPG.GameSessions.Commands;

internal class EndGameSessionCommand
{
    public required Guid SessionId { get; init; }
}

internal class EndGameSessionCommandHandler(
    ICommandHandler<SetWorldPlaytimeCommand> setWorldPlaytime,
    IQueryHandler<GetGameSessionQuery, GameSession> getGameSession,
    ICommandHandler<DeleteGameSessionCommand> deleteGameSession,
    ICommandHandler<AbandonActiveFightCommand> abandonActiveFight,
    IMemoryCache cache
) : ICommandHandler<EndGameSessionCommand>
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

        cache.Remove(GetLoreAnchorsByWorldQueryHandler.CacheKey(snapshot.WorldId));
        cache.Remove(GetLoreAnchorAutomatonByWorldQueryHandler.CacheKey(snapshot.WorldId));
    }
}
