using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Navigation;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Routing.Commands;

public class PlanCircuitJourneyCommand
{
    public required Guid TravelCircuitId { get; init; }
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
    public required GameInstant PlannedAt { get; init; }
    public required string Purpose { get; init; }
}

internal class PlanCircuitJourneyCommandHandler(
    ICreaturesDbContext creatures,
    IWorldsDbContext worlds,
    IRoutingDbContext routing
) : ICommandHandler<PlanCircuitJourneyCommand, Guid>
{
    public async Task<Guid> Handle(
        PlanCircuitJourneyCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var circuit = await routing.TravelCircuits.FindAsync(
            [command.TravelCircuitId],
            cancellationToken
        );
        if (circuit is null)
        {
            throw new InvalidOperationException("The travel circuit does not exist.");
        }

        var circuitLegs = await routing
            .TravelCircuitLegs.AsNoTracking()
            .Where(leg => leg.TravelCircuitId == circuit.Id)
            .OrderBy(leg => leg.Index)
            .ToArrayAsync(cancellationToken);
        var members = await LoadMembers(command.CreatureIds, circuitLegs, cancellationToken);
        var graph = await LoadGraph(circuit.WorldId, cancellationToken);
        var legs = circuitLegs
            .Select(leg => graph.SnapshotLeg(leg.FromNodeId, leg.ToNodeId, leg.ConnectorId))
            .ToArray();
        var journey = new Journey
        {
            WorldId = circuit.WorldId,
            TravelCircuitId = circuit.Id,
            Purpose = command.Purpose,
            Status = JourneyStatus.Planned,
            PlannedAt = command.PlannedAt,
            DepartureAt = command.PlannedAt,
            CheckpointLegIndex = 0,
            CheckpointLegProgressMeters = 0,
            CheckpointedAt = command.PlannedAt,
        };

        routing.Journeys.Add(journey);
        routing.JourneyMembers.AddRange(
            members.Select(member => new JourneyMember
            {
                JourneyId = journey.Id,
                CreatureId = member.Id,
            })
        );
        routing.JourneyLegs.AddRange(
            legs.Select(
                (leg, index) =>
                    new JourneyLeg
                    {
                        JourneyId = journey.Id,
                        Index = index,
                        FromNodeId = leg.FromNodeId,
                        ToNodeId = leg.ToNodeId,
                        ConnectorId = leg.ConnectorId,
                        Distance = leg.Distance,
                        Path = new Polyline
                        {
                            Points =
                            [
                                .. leg.Path.Points.Select(point => new Point(point.X, point.Y)),
                            ],
                        },
                        DwellAfter = circuitLegs[index].DwellAfter,
                    }
            )
        );
        await routing.SaveChangesAsync(cancellationToken);

        return journey.Id;
    }

    private async Task<IReadOnlyCollection<Creature>> LoadMembers(
        IReadOnlyCollection<Guid> creatureIds,
        IReadOnlyList<TravelCircuitLeg> circuitLegs,
        CancellationToken cancellationToken
    )
    {
        if (creatureIds.Count == 0 || circuitLegs.Count == 0)
        {
            throw new InvalidOperationException("A circuit journey needs members and legs.");
        }

        var members = await creatures
            .Creatures.Where(creature => creatureIds.AsEnumerable().Contains(creature.Id))
            .ToArrayAsync(cancellationToken);
        if (
            members.Length != creatureIds.Count
            || members.Any(member => member.CurrentTravelNodeId != circuitLegs[0].FromNodeId)
        )
        {
            throw new InvalidOperationException(
                "Circuit members must be at the circuit's first node."
            );
        }

        return members;
    }

    private async Task<TravelGraph> LoadGraph(Guid worldId, CancellationToken cancellationToken)
    {
        var locationConnectors = await worlds
            .LocationConnectors.AsNoTracking()
            .Where(connector => connector.WorldId == worldId)
            .ToArrayAsync(cancellationToken);
        var pointConnectors = await worlds
            .PointConnectors.AsNoTracking()
            .Where(connector => connector.WorldId == worldId)
            .ToArrayAsync(cancellationToken);
        var nodes = await worlds
            .TravelNodes.AsNoTracking()
            .Where(node => node.WorldId == worldId)
            .ToArrayAsync(cancellationToken);

        return new TravelGraph([.. locationConnectors, .. pointConnectors], nodes);
    }
}
