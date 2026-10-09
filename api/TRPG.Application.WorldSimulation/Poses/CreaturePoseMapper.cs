using TRPG.Application.Creatures.Commands;
using TRPG.Application.WorldSimulation.Movement;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldSimulation.Poses;

public sealed class CreaturePoseMapper(IEnumerable<PlacedConnector> connectors)
{
    private readonly Dictionary<Guid, PlacedConnector> _connectorsById = connectors.ToDictionary(
        placed => placed.Connector.Id
    );

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
                null,
                new WalkColumns(
                    null,
                    null,
                    started.StopPosition ?? ExitOf(started.NextConnectorId),
                    started.At
                )
            ),
            LocationEntered entered => new CreaturePoseUpdate(
                entered.CreatureId,
                entered.ToLocationId,
                entered.FromLocationId,
                CreatureMovement.Walking,
                null,
                new WalkColumns(
                    _connectorsById[entered.ConnectorId].Arrival,
                    entered.At,
                    entered.StopPosition ?? ExitOf(entered.NextConnectorId),
                    entered.NextConnectorId == null ? null : entered.At
                )
            ),
            JourneyCompleted completed => new CreaturePoseUpdate(
                completed.CreatureId,
                completed.LocationId,
                null,
                CreatureMovement.Stationary,
                completed.Action.ToActivity(),
                null
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(simEvent)),
        };

    private Point? ExitOf(Guid? connectorId) =>
        connectorId == null ? null : _connectorsById[connectorId.Value].Exit;
}
