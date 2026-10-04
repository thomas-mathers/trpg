using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

public record CreatureLayoutInput(
    IReadOnlyList<Location> Locations,
    IReadOnlyList<Prop> Props,
    IReadOnlyList<Building> Buildings,
    IReadOnlyList<LocationConnector> Connectors,
    IReadOnlyList<Creature> Creatures
)
{
    public IReadOnlyList<Creature> AlreadyPlaced { get; init; } = [];
}

internal sealed class CreatureLayoutContext
{
    private readonly ILookup<Guid, Prop> _propsByLocation;
    private readonly ILookup<Guid, Building> _buildingsByExterior;
    private readonly ILookup<Guid, LocationConnector> _connectorsByOrigin;
    private readonly IReadOnlyList<LocationConnector> _connectors;
    private readonly Dictionary<Guid, List<PlacementObstacle>> _creaturesByLocation;

    internal CreatureLayoutContext(CreatureLayoutInput input)
    {
        LocationById = input.Locations.ToDictionary(location => location.Id);
        _propsByLocation = input.Props.ToLookup(prop => prop.LocationId);
        _buildingsByExterior = input.Buildings.ToLookup(building => building.ExteriorLocationId);
        _connectors = input.Connectors;
        _connectorsByOrigin = input.Connectors.ToLookup(connector => connector.OriginLocationId);
        _creaturesByLocation = input
            .AlreadyPlaced.GroupBy(creature => creature.LocationId)
            .ToDictionary(group => group.Key, group => group.Select(CreatureObstacle).ToList());
    }

    internal IReadOnlyDictionary<Guid, Location> LocationById { get; }

    internal IEnumerable<Prop> PropsAt(Guid locationId) => _propsByLocation[locationId];

    internal PlacementObstacle[] ObstaclesAt(Guid locationId, Guid? excludedPropId)
    {
        var doors = _connectorsByOrigin[locationId].Select(DoorObstacle);

        return [.. SolidsAt(locationId, excludedPropId), .. doors];
    }

    internal PlacementObstacle[] ArrivalObstaclesAt(Guid locationId) =>
        SolidsAt(locationId, excludedPropId: null);

    private PlacementObstacle[] SolidsAt(Guid locationId, Guid? excludedPropId)
    {
        var props = _propsByLocation[locationId]
            .Where(prop => prop.Id != excludedPropId && prop.Width > 0)
            .Select(PropObstacle);
        var buildings = _buildingsByExterior[locationId]
            .Select(building => new PlacementObstacle(
                new Placement(building.X, building.Y, building.Angle),
                new Footprint(building.Width, building.Depth)
            ));
        var creatures = _creaturesByLocation.GetValueOrDefault(locationId) ?? [];

        return [.. props, .. buildings, .. creatures];
    }

    internal Placement? ArrivalPointFrom(Guid originLocationId, Guid destinationLocationId)
    {
        var connector = _connectors.FirstOrDefault(candidate =>
            candidate.OriginLocationId == originLocationId
            && candidate.DestinationLocationId == destinationLocationId
        );

        return connector is null
            ? null
            : new Placement(connector.ArrivalX, connector.ArrivalY, connector.ArrivalAngle);
    }

    internal void Record(Creature creature)
    {
        if (!_creaturesByLocation.TryGetValue(creature.LocationId, out var placed))
        {
            placed = [];
            _creaturesByLocation[creature.LocationId] = placed;
        }

        placed.Add(CreatureObstacle(creature));
    }

    internal static Placement PoseOf(Prop prop) => new(prop.X, prop.Y, prop.Angle);

    private static PlacementObstacle DoorObstacle(LocationConnector connector)
    {
        var box = ExitKeepOut.Of(
            new PlanarPoint(connector.ExitX, connector.ExitY),
            connector.ExitAngle,
            connector.StairDirection
        );

        return new PlacementObstacle(
            new Placement(box.CenterX, box.CenterY, 0),
            new Footprint(box.Width, box.Depth)
        );
    }

    private static PlacementObstacle PropObstacle(Prop prop) =>
        new(PoseOf(prop), new Footprint(prop.Width, prop.Depth));

    private static PlacementObstacle CreatureObstacle(Creature creature) =>
        new(new Placement(creature.X, creature.Y, creature.Angle), CreaturePlacementResolver.Body);
}
