using TRPG.Application.Caravans.Commands;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Creatures.Results;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Worlds.Commands;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal class BoardCaravanActionHandler(
    GameActionRunner actionRunner,
    GameTurnContext turnContext,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetGameTimeQuery, GameInstant> getGameTime,
    ICommandHandler<BoardCaravanCommand, BoardCaravanResult> boardCaravan,
    ICommandHandler<AdvanceTimeCommand, GameInstant> advanceTime,
    ICommandHandler<
        AdvanceCreatureEffectsCommand,
        IReadOnlyCollection<CreatureVitals>
    > advanceCreatureEffects,
    ICommandHandler<
        ApplyPassiveRegenCommand,
        IReadOnlyDictionary<Guid, Creature>
    > applyPassiveRegen,
    ICommandHandler<MovePlayerCommand> movePlayer,
    IQueryHandler<GetActiveEncounterQuery, Encounter?> getActiveEncounter,
    ICommandHandler<PublishEncounterStartedCommand> publishEncounterStarted
)
{
    public Task<ActionOutcome> Handle(
        GameTurnSession session,
        Guid caravanId,
        CancellationToken cancellationToken = default
    ) => actionRunner.Run(session, ct => Resolve(session, caravanId, ct), cancellationToken);

    private async Task<ActionOutcome> Resolve(
        GameTurnSession session,
        Guid caravanId,
        CancellationToken cancellationToken
    )
    {
        var player =
            await getCreatureById.Handle(
                new GetCreatureByIdQuery { Id = session.PlayerId },
                cancellationToken
            ) ?? throw new EntityNotFoundException(nameof(Creature), session.PlayerId);
        if (player.HasActiveDots)
        {
            return ActionOutcome.Failed(ActionFailure.Afflicted);
        }
        var gameTime = await getGameTime.Handle(
            new GetGameTimeQuery { SessionId = session.SessionId },
            cancellationToken
        );

        var result = await boardCaravan.Handle(
            new BoardCaravanCommand
            {
                PlayerId = session.PlayerId,
                CaravanId = caravanId,
                PlayerLocationId = player.LocationId,
                GameTime = gameTime,
            },
            cancellationToken
        );

        if (result.Outcome == BoardCaravanOutcome.NoTicket)
        {
            return ActionOutcome.Failed(ActionFailure.NoTicket);
        }
        if (result.Outcome == BoardCaravanOutcome.CaravanNotPresent)
        {
            return ActionOutcome.Failed(ActionFailure.CaravanNotPresent);
        }
        if (result.Outcome == BoardCaravanOutcome.TravelSuspended)
        {
            return ActionOutcome.Failed(ActionFailure.TravelSuspended);
        }

        var arrivalGameTime = await advanceTime.Handle(
            new AdvanceTimeCommand
            {
                WorldId = session.WorldId,
                Delta = TimeSpan.FromHours(1) * result.TravelTimeHours!.Value,
            },
            cancellationToken
        );

        await advanceCreatureEffects.Handle(
            new AdvanceCreatureEffectsCommand
            {
                WorldId = session.WorldId,
                CreatureIds = [session.PlayerId],
                GameTime = arrivalGameTime,
            },
            cancellationToken
        );

        await applyPassiveRegen.Handle(
            new ApplyPassiveRegenCommand
            {
                GameTime = arrivalGameTime,
                CreatureIds = [session.PlayerId],
            },
            cancellationToken
        );

        await movePlayer.Handle(
            new MovePlayerCommand
            {
                PlayerId = session.PlayerId,
                DestinationLocationId = result.DestinationLocationId!.Value,
                GameTime = arrivalGameTime,
            },
            cancellationToken
        );
        turnContext.PlayerMoved = true;

        var startedEncounter = await getActiveEncounter.Handle(
            new GetActiveEncounterQuery { PlayerId = session.PlayerId },
            cancellationToken
        );

        await publishEncounterStarted.Handle(
            new PublishEncounterStartedCommand
            {
                PlayerId = session.PlayerId,
                Encounter = startedEncounter,
                GameTime = arrivalGameTime,
            },
            cancellationToken
        );

        return ActionOutcome.Success;
    }
}
