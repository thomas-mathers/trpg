using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Creatures.Events;
using TRPG.Application.Creatures.Results;
using TRPG.Application.Worlds.Commands;

namespace TRPG.Application.WorldSimulation;

internal class PlayerVitalsPublisher(
    ICommandHandler<StampWorldStateCommand, WorldStateStamp> stampWorldState,
    IGameClientEventSink gameEvents
)
{
    public async Task Publish(
        Guid worldId,
        IReadOnlyCollection<Guid> playerIds,
        IReadOnlyCollection<CreatureVitals> changedVitals,
        CancellationToken cancellationToken
    )
    {
        var playerVitals = changedVitals
            .Where(vitals => playerIds.Contains(vitals.CreatureId))
            .ToArray();
        if (playerVitals.Length == 0)
        {
            return;
        }

        // Stamped after the mutation so the version orders these vitals against any snapshot.
        var stamp = await stampWorldState.Handle(
            new StampWorldStateCommand { WorldId = worldId },
            cancellationToken
        );
        foreach (var vitals in playerVitals)
        {
            gameEvents.Enqueue(new PlayerVitalsChangedEvent(worldId, vitals, stamp.Version));
        }
    }
}
