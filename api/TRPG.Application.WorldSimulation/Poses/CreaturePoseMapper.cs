using TRPG.Application.Creatures.Commands;
using TRPG.Application.WorldSimulation.Movement;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldSimulation.Poses;

public sealed class CreaturePoseMapper
{
    public IReadOnlyList<CreaturePoseUpdate> Map(IEnumerable<SimEvent> events) =>
        [
            .. events
                .Select(MapEvent)
                .GroupBy(update => update.CreatureId)
                .Select(group => group.Aggregate((merged, next) => merged.Then(next))),
        ];

    public CreaturePoseUpdate MapEvent(SimEvent simEvent) =>
        simEvent switch
        {
            JourneyStarted started => new CreaturePoseUpdate(
                started.CreatureId,
                started.OriginLocationId,
                null,
                CreatureMovement.Walking,
                null
            ),
            LocationEntered entered => new CreaturePoseUpdate(
                entered.CreatureId,
                entered.ToLocationId,
                entered.FromLocationId,
                CreatureMovement.Walking,
                null,
                CurrentTravelNodeId: entered.ArrivalNodeId
            ),
            JourneyCompleted completed => new CreaturePoseUpdate(
                completed.CreatureId,
                completed.LocationId,
                null,
                CreatureMovement.Stationary,
                completed.Action.ToActivity(),
                CurrentTravelNodeId: completed.ArrivalNodeId
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(simEvent)),
        };
}
