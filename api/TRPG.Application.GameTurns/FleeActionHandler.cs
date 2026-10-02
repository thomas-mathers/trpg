using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.GameSessions.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal class FleeActionHandler(
    GameActionRunner actionRunner,
    ICommandHandler<ResolveFleeCombatCommand, FleeCombatResult?> resolveFleeCombat,
    ICommandHandler<MovePlayerCommand> movePlayer,
    IQueryHandler<GetActiveEncounterQuery, Encounter?> getActiveEncounter,
    ICommandHandler<PublishEncounterStartedCommand> publishEncounterStarted,
    IQueryHandler<GetGameTimeQuery, GameInstant> getGameTime
)
{
    public Task<ActionOutcome> Handle(
        GameTurnSession session,
        CancellationToken cancellationToken = default
    ) => actionRunner.Run(session, ct => Resolve(session, ct), cancellationToken);

    private async Task<ActionOutcome> Resolve(
        GameTurnSession session,
        CancellationToken cancellationToken
    )
    {
        var gameTime = await getGameTime.Handle(
            new GetGameTimeQuery { SessionId = session.SessionId },
            cancellationToken
        );

        var result = await resolveFleeCombat.Handle(
            new ResolveFleeCombatCommand
            {
                SessionId = session.SessionId,
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                GameTime = gameTime,
            },
            cancellationToken
        );

        if (result == null)
        {
            return ActionOutcome.Failed(ActionFailure.NoFight);
        }

        if (
            result.CombatResult.Outcome != CombatOutcome.Fled
            || result.DestinationLocationId is not { } destinationLocationId
        )
        {
            return ActionOutcome.Success;
        }

        await movePlayer.Handle(
            new MovePlayerCommand
            {
                PlayerId = session.PlayerId,
                DestinationLocationId = destinationLocationId,
                GameTime = gameTime,
            },
            cancellationToken
        );

        var startedEncounter = await getActiveEncounter.Handle(
            new GetActiveEncounterQuery { PlayerId = session.PlayerId },
            cancellationToken
        );

        await publishEncounterStarted.Handle(
            new PublishEncounterStartedCommand
            {
                PlayerId = session.PlayerId,
                Encounter = startedEncounter,
                GameTime = gameTime,
            },
            cancellationToken
        );

        return ActionOutcome.Success;
    }
}
