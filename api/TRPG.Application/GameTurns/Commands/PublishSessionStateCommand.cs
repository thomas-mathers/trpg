using TRPG.Application.Combat;
using TRPG.Application.Combat.Queries;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Mappers;
using TRPG.Application.Encounters;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.Scenes;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.Worlds.Queries;

namespace TRPG.Application.GameTurns.Commands;

internal class PublishSessionStateCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid SessionId { get; init; }
}

internal class PublishSessionStateCommandHandler(
    IGameClientEventSink gameEvents,
    IGameClientEventDispatcher eventDispatcher,
    GetCurrentSceneQueryHandler getCurrentScene,
    GetActiveFightCombatantsQueryHandler getActiveFightCombatants,
    GetActiveEncounterQueryHandler getActiveEncounter,
    GetEncounterGroupContextQueryHandler getEncounterGroupContext,
    GetLocationByIdQueryHandler getLocationById
)
{
    public async Task Handle(
        PublishSessionStateCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var scene = await getCurrentScene.Handle(
            new GetCurrentSceneQuery
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                SessionId = command.SessionId,
            },
            cancellationToken
        );
        gameEvents.Enqueue(
            new SceneUpdatedEvent(
                SceneSnapshotMapper.ToSnapshot(scene),
                SceneUpdateReason.Reconnected
            )
        );

        var combatants = await getActiveFightCombatants.Handle(
            new GetActiveFightCombatantsQuery { PlayerId = command.PlayerId },
            cancellationToken
        );
        if (combatants.Count > 0)
        {
            gameEvents.Enqueue(
                new CombatStartedEvent(CombatantStateMapper.ToCombatantStates(combatants))
            );
        }

        var encounter = await getActiveEncounter.Handle(
            new GetActiveEncounterQuery { PlayerId = command.PlayerId },
            cancellationToken
        );
        if (encounter != null)
        {
            var groupContext = await getEncounterGroupContext.Handle(
                new GetEncounterGroupContextQuery { EncounterGroupId = encounter.EncounterGroupId },
                cancellationToken
            );
            var location = await getLocationById.Handle(
                new GetLocationByIdQuery { Id = encounter.LocationId },
                cancellationToken
            );
            gameEvents.Enqueue(
                new EncounterStartedEvent(
                    HostileEncounterStateMapper.ToState(
                        encounter.Id,
                        groupContext.Faction,
                        groupContext.LivingMembers,
                        location!
                    )
                )
            );
        }

        await eventDispatcher.FlushAsync(command.WorldId, cancellationToken);
    }
}
