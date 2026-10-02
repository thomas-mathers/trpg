using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal class StreamRespawnTurnHandler(
    GameTurnStreamer streamer,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    PlayerRespawner playerRespawner
)
{
    public IAsyncEnumerable<string> Handle(
        GameTurnSession session,
        CancellationToken cancellationToken = default
    ) => streamer.StreamTurn(session, ct => ResolveTurn(session, ct), cancellationToken);

    private async Task<GameTurnPrompt> ResolveTurn(
        GameTurnSession session,
        CancellationToken cancellationToken
    )
    {
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = session.PlayerId },
            cancellationToken
        );

        if (player?.Condition != CreatureCondition.Dead)
        {
            return new GameTurnPrompt.Reply("There's nothing to respawn from right now.");
        }

        await playerRespawner.Respawn(session, cancellationToken);

        return new GameTurnPrompt.None();
    }
}
