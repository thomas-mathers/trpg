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
                entered.StopPosition,
                CurrentTravelNodeId: entered.ArrivalNodeId
            ),
            JourneyLegCompleted completed => new CreaturePoseUpdate(
                completed.CreatureId,
                completed.LocationId,
                null,
                CreatureMovement.Walking,
                null,
                completed.StopPosition,
                completed.ArrivalNodeId
            ),
            JourneyCompleted completed => new CreaturePoseUpdate(
                completed.CreatureId,
                completed.LocationId,
                null,
                CreatureMovement.Stationary,
                completed.Action.ToActivity(),
                completed.StopPosition,
                CurrentTravelNodeId: completed.ArrivalNodeId
            ),
            LocalMoveStarted started => new CreaturePoseUpdate(
                started.CreatureId,
                started.LocationId,
                null,
                CreatureMovement.Walking,
                null,
                started.Move.Path[0]
            ),
            LocalMoveCompleted completed => new CreaturePoseUpdate(
                completed.CreatureId,
                completed.LocationId,
                null,
                CreatureMovement.Stationary,
                completed.Move.Action.ToActivity(),
                completed.StopPosition
            ),
            LocalMoveInterrupted interrupted => new CreaturePoseUpdate(
                interrupted.CreatureId,
                interrupted.LocationId,
                null,
                CreatureMovement.Stationary,
                null,
                interrupted.StopPosition
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(simEvent)),
        };
}
