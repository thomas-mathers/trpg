using TRPG.Application.Combat.Commands;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Encounters.Events;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.GameTurns.Events;
using TRPG.Application.Scenes.Queries;
using TRPG.Domain.Models;

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
    ICommandHandler<PublishCombatStateCommand> publishCombatState,
    IQueryHandler<GetActiveEncounterQuery, Encounter?> getActiveEncounter
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

        await publishCombatState.Handle(
            new PublishCombatStateCommand { PlayerId = command.PlayerId },
            cancellationToken
        );

        var encounter = await getActiveEncounter.Handle(
            new GetActiveEncounterQuery { PlayerId = command.PlayerId },
            cancellationToken
        );
        if (encounter is HostileEncounter hostileEncounter)
        {
            gameEvents.Enqueue(new EncounterStartedEvent(hostileEncounter));
        }
        else if (encounter is GuardEncounter guardEncounter)
        {
            gameEvents.Enqueue(new GuardEncounterStartedEvent(guardEncounter));
        }

        await eventDispatcher.FlushAsync(command.WorldId, cancellationToken);
    }
}
