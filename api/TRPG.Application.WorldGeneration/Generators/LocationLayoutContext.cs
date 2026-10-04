using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal record LocationLayoutInput(
    IReadOnlyCollection<Location> Locations,
    IReadOnlyCollection<District> Districts,
    IReadOnlyCollection<Prop> Props,
    IReadOnlyCollection<Building> Buildings,
    IReadOnlyCollection<Room> Rooms,
    IReadOnlyCollection<LocationConnector> Connectors,
    IReadOnlyCollection<State> States
);

internal sealed class LocationLayoutContext(LocationLayoutInput input)
{
    internal IReadOnlyDictionary<Guid, Location> LocationById { get; } =
        input.Locations.ToDictionary(location => location.Id);

    internal IReadOnlyDictionary<Guid, Room> RoomByLocationId { get; } =
        input.Rooms.ToDictionary(room => room.LocationId);

    internal IReadOnlyDictionary<Guid, Building> BuildingById { get; } =
        input.Buildings.ToDictionary(building => building.Id);

    internal IReadOnlyDictionary<Guid, District> DistrictByLocationId { get; } =
        input.Districts.ToDictionary(district => district.LocationId);

    internal IReadOnlyDictionary<Guid, State> StateById { get; } =
        input.States.ToDictionary(state => state.Id);

    internal ILookup<Guid, Prop> PropsByLocationId { get; } =
        input.Props.ToLookup(prop => prop.LocationId);

    internal ILookup<Guid, LocationConnector> ConnectorsByOrigin { get; } =
        input.Connectors.ToLookup(connector => connector.OriginLocationId);

    internal ILookup<Guid, Building> BuildingsByExterior { get; } =
        input.Buildings.ToLookup(building => building.ExteriorLocationId);

    internal ILookup<Guid, Room> RoomsByBuilding { get; } =
        input.Rooms.ToLookup(room => room.BuildingId);

    internal IReadOnlyCollection<Location> Locations => input.Locations;

    internal IReadOnlyCollection<LocationConnector> Connectors => input.Connectors;

    internal IReadOnlyCollection<Building> Buildings => input.Buildings;

    internal static bool IsHallway(Room room) => room.Name == BuildingGenerator.HallwayName;
}

internal static class LayoutSeed
{
    internal static int From(Guid id) => id.GetHashCode();

    internal static double PairBearing(Guid originId, Guid destinationId)
    {
        var originIsLow = originId.CompareTo(destinationId) < 0;
        var low = originIsLow ? originId : destinationId;
        var high = originIsLow ? destinationId : originId;
        var bearing =
            new Random(unchecked(low.GetHashCode() * 31 + high.GetHashCode())).NextDouble()
            * 2
            * Math.PI;

        return originIsLow ? bearing : (bearing + Math.PI) % (2 * Math.PI);
    }
}
