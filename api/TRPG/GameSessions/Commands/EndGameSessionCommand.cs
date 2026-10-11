using TRPG.Application.Common.Clocks;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.GameSessions.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Narration.Commands;
using TRPG.Application.WorldSimulation;
using TRPG.Domain.Models;

namespace TRPG.GameSessions.Commands;

internal class EndGameSessionCommand
{
    public required Guid SessionId { get; init; }
}

internal class EndGameSessionCommandHandler(
    IWorldClock worldClock,
    WorldSimulationCoordinator simulation,
    IQueryHandler<GetGameSessionQuery, GameSession> getGameSession,
    ICommandHandler<DeleteGameSessionCommand> deleteGameSession,
    ICommandHandler<ClearNonEncounterEngagementsCommand> clearNonEncounterEngagements,
    ICommandHandler<InvalidateWorldLoreAnchorsCommand> invalidateWorldLoreAnchors
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

        await simulation.Request(
            snapshot.WorldId,
            (_, token) => End(command.SessionId, snapshot.WorldId, token),
            cancellationToken
        );
    }

    private async Task End(Guid sessionId, Guid worldId, CancellationToken cancellationToken)
    {
        var gameTime = await worldClock.GetCurrent(worldId, cancellationToken);

        await clearNonEncounterEngagements.Handle(
            new ClearNonEncounterEngagementsCommand { WorldId = worldId, GameTime = gameTime },
            cancellationToken
        );

        await deleteGameSession.Handle(
            new DeleteGameSessionCommand { SessionId = sessionId },
            cancellationToken
        );

        await invalidateWorldLoreAnchors.Handle(
            new InvalidateWorldLoreAnchorsCommand { WorldId = worldId },
            cancellationToken
        );
    }
}
