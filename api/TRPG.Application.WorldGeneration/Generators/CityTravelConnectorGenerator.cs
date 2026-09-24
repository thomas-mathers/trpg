using TRPG.Application.Configuration;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class CityTravelConnectorGenerator
{
    public static IReadOnlyList<TravelConnector> Generate(
        Guid worldId,
        IReadOnlyCollection<District> districts,
        IReadOnlyCollection<Building> buildings,
        IReadOnlyCollection<Room> rooms,
        IReadOnlyCollection<LocationConnector> locationConnectors,
        CityTravelOptions options
    )
    {
        var districtByLocationId = districts.ToDictionary(district => district.LocationId);
        var roomByLocationId = rooms.ToDictionary(room => room.LocationId);
        var buildingById = buildings.ToDictionary(building => building.Id);

        return locationConnectors
            .Select(connector =>
                ResolveDistance(
                    connector,
                    districtByLocationId,
                    roomByLocationId,
                    buildingById,
                    options
                )
            )
            .Where(result => result != null)
            .Select(result => new TravelConnector
            {
                WorldId = worldId,
                ConnectorId = result!.ConnectorId,
                Distance = result.Distance,
            })
            .ToArray();
    }

    private static CityTravelDistance? ResolveDistance(
        LocationConnector connector,
        IReadOnlyDictionary<Guid, District> districtByLocationId,
        IReadOnlyDictionary<Guid, Room> roomByLocationId,
        IReadOnlyDictionary<Guid, Building> buildingById,
        CityTravelOptions options
    )
    {
        if (
            districtByLocationId.TryGetValue(connector.OriginLocationId, out var originDistrict)
            && districtByLocationId.TryGetValue(
                connector.DestinationLocationId,
                out var destinationDistrict
            )
            && originDistrict.CityId == destinationDistrict.CityId
        )
        {
            return new CityTravelDistance(connector.Id, options.DistrictDistance);
        }

        if (
            IsBuildingEntrance(
                connector.OriginLocationId,
                connector.DestinationLocationId,
                districtByLocationId,
                roomByLocationId,
                buildingById
            )
            || IsBuildingEntrance(
                connector.DestinationLocationId,
                connector.OriginLocationId,
                districtByLocationId,
                roomByLocationId,
                buildingById
            )
        )
        {
            return new CityTravelDistance(connector.Id, options.BuildingDistance);
        }

        if (
            roomByLocationId.TryGetValue(connector.OriginLocationId, out var originRoom)
            && roomByLocationId.TryGetValue(
                connector.DestinationLocationId,
                out var destinationRoom
            )
            && originRoom.BuildingId == destinationRoom.BuildingId
        )
        {
            return new CityTravelDistance(connector.Id, Distance: 0);
        }

        return null;
    }

    private static bool IsBuildingEntrance(
        Guid exteriorLocationId,
        Guid roomLocationId,
        IReadOnlyDictionary<Guid, District> districtByLocationId,
        IReadOnlyDictionary<Guid, Room> roomByLocationId,
        IReadOnlyDictionary<Guid, Building> buildingById
    ) =>
        districtByLocationId.ContainsKey(exteriorLocationId)
        && roomByLocationId.TryGetValue(roomLocationId, out var room)
        && buildingById.TryGetValue(room.BuildingId, out var building)
        && building.ExteriorLocationId == exteriorLocationId;

    private record CityTravelDistance(Guid ConnectorId, float Distance);
}
