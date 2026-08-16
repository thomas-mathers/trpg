using TRPG.Application.Combat;
using TRPG.Application.Combat.Events;
using TRPG.Application.Combat.Mappers;
using TRPG.Application.Combat.Queries;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Handling;
using TRPG.Application.Encounters;
using TRPG.Application.Encounters.Events;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.GameTurns.Events;
using TRPG.Application.Scenes;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Data.Models;

namespace TRPG.Application.GameTurns.Commands;

public class PublishSessionStateCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid SessionId { get; init; }
}

internal class PublishSessionStateCommandHandler(
    IGameClientEventSink gameEvents,
    IGameClientEventDispatcher eventDispatcher,
    IQueryHandler<GetCurrentSceneQuery, SceneResult> getCurrentScene,
    IQueryHandler<GetActiveFightCombatantsQuery, IReadOnlyList<Combatant>> getActiveFightCombatants,
    IQueryHandler<GetActiveEncounterQuery, HostileEncounter?> getActiveEncounter,
    IQueryHandler<GetHostileEncounterStateQuery, HostileEncounterState> getHostileEncounterState
) : ICommandHandler<PublishSessionStateCommand>
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
        gameEvents.Enqueue(new SceneUpdatedEvent(scene));

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
            var state = await getHostileEncounterState.Handle(
                new GetHostileEncounterStateQuery
                {
                    EncounterId = encounter.Id,
                    EncounterGroupId = encounter.EncounterGroupId,
                    LocationId = encounter.LocationId,
                },
                cancellationToken
            );
            gameEvents.Enqueue(new EncounterStartedEvent(state));
        }

        await eventDispatcher.FlushAsync(command.WorldId, cancellationToken);
    }
}
