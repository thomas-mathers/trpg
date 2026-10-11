using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Creatures.Events;
using TRPG.Application.Creatures.Results;
using TRPG.Application.Worlds.Commands;

namespace TRPG.Application.WorldSimulation.Publishing;

internal class CreatureVitalsPublisher(
    ICommandHandler<StampWorldStateCommand, WorldStateStamp> stampWorldState,
    IGameClientEventSink gameEvents
)
{
    public async Task Publish(
        Guid worldId,
        Guid locationId,
        IReadOnlyCollection<Guid> watcherIds,
        IReadOnlyCollection<CreatureVitals> changedVitals,
        CancellationToken cancellationToken
    )
    {
        if (changedVitals.Count == 0 || watcherIds.Count == 0)
        {
            return;
        }

        // Stamped after the mutation so the version orders these vitals against any snapshot.
        var stamp = await stampWorldState.Handle(
            new StampWorldStateCommand { WorldId = worldId },
            cancellationToken
        );
        gameEvents.Enqueue(
            new CreatureVitalsChangedEvent(worldId, locationId, changedVitals, stamp.Version)
        );
    }
}
