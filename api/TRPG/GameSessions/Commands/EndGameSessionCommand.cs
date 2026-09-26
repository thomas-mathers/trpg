using TRPG.Application.Common.Clocks;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Concurrency;
using TRPG.Application.Common.Queries;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.GameSessions.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Narration.Commands;
using TRPG.Domain.Models;

namespace TRPG.GameSessions.Commands;

internal class EndGameSessionCommand
{
    public required Guid SessionId { get; init; }
}

internal class EndGameSessionCommandHandler(
    IWorldClock worldClock,
    IWorldMutationGate mutationGate,
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

        await using var lease = await mutationGate.Acquire(snapshot.WorldId, cancellationToken);

        var gameTime = await worldClock.GetCurrent(snapshot.WorldId, cancellationToken);

        await clearNonEncounterEngagements.Handle(
            new ClearNonEncounterEngagementsCommand
            {
                WorldId = snapshot.WorldId,
                GameTime = gameTime,
            },
            cancellationToken
        );

        await deleteGameSession.Handle(
            new DeleteGameSessionCommand { SessionId = command.SessionId },
            cancellationToken
        );

        await invalidateWorldLoreAnchors.Handle(
            new InvalidateWorldLoreAnchorsCommand { WorldId = snapshot.WorldId },
            cancellationToken
        );
    }
}
