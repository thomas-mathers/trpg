using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Helpers;

internal static class MiniLayoutWorldBuilder
{
    private static readonly BuildingType[] CityBuildingTypes = Enum.GetValues<BuildingType>()
        .Where(type => !BuildingTypes.Dungeon.Contains(type))
        .ToArray();

    internal static MiniLayoutWorld BuildWorld(int iteration)
    {
        var worldId = Guid.NewGuid();
        var stateA = MakeState(worldId, new Point(10, 10));
        var stateB = MakeState(worldId, new Point(40, -20));
        var wildernessA = MakeWilderness(worldId, stateA.Id);
        var wildernessB = MakeWilderness(worldId, stateB.Id);
        var cityId = Guid.NewGuid();
        var districts = new[] { DistrictType.CityCenter, DistrictType.Residential }
            .Select(type => DistrictGenerator.Generate(type, cityId, stateA.Id, worldId))
            .ToArray();
        List<Location> locations =
        [
            wildernessA,
            wildernessB,
            .. districts.Select(district => district.Location),
        ];
        var props = new List<Prop>(districts.SelectMany(district => district.Seats));
        var rooms = new List<Room>();
        var buildings = new List<Building>();
        var connectors = new List<LocationConnector>
        {
            MakeConnector(worldId, districts[0].Location, districts[1].Location, "Path"),
            MakeConnector(worldId, districts[1].Location, districts[0].Location, "Path"),
            MakeConnector(worldId, districts[0].Location, wildernessA, "Path"),
            MakeConnector(worldId, wildernessA, districts[0].Location, "Path"),
            MakeConnector(worldId, wildernessA, wildernessB, "Trail"),
            MakeConnector(worldId, wildernessB, wildernessA, "Trail"),
        };

        foreach (var type in CityBuildingTypes)
        {
            var spec = BuildingSpecCatalog.GetSpecs(
                type,
                Guid.NewGuid(),
                [Guid.NewGuid()],
                bedroomGroups: null
            );
            var result = new BuildingGenerator().Generate(
                new BuildingGeneratorInput(districts[(int)type % 2].Location, spec)
                {
                    Name = type.ToString(),
                }
            );
            buildings.Add(result.Building);
            rooms.AddRange(result.Rooms);
            locations.AddRange(result.Locations);
            props.AddRange(result.Props);
            connectors.AddRange(result.LocationConnectors);
        }

        foreach (var type in BuildingTypes.Dungeon)
        {
            var dungeon = DungeonGenerator.Generate(
                new DungeonGeneratorInput([], wildernessA, worldId)
                {
                    BuildingType = type,
                    Random = new Random(iteration * 100 + (int)type),
                }
            );
            buildings.Add(dungeon.Building);
            rooms.AddRange(dungeon.Rooms);
            locations.AddRange(dungeon.Locations);
            connectors.AddRange(dungeon.LocationConnectors);
        }

        return new MiniLayoutWorld(
            new LocationLayoutInput(
                locations,
                props,
                buildings,
                rooms,
                connectors,
                [stateA, stateB]
            )
        );
    }

    internal static MiniLayoutWorld BuildHouseWorld(IReadOnlyList<Guid> memberIds)
    {
        var worldId = Guid.NewGuid();
        var state = MakeState(worldId, new Point(10, 10));
        var district = DistrictGenerator.Generate(
            DistrictType.Residential,
            Guid.NewGuid(),
            state.Id,
            worldId
        );
        var spec = BuildingSpecCatalog.GetSpecs(
            BuildingType.House,
            memberIds[0],
            memberIds,
            HouseholdBedroomGroups(memberIds)
        );
        var result = new BuildingGenerator().Generate(
            new BuildingGeneratorInput(district.Location, spec) { Name = "House" }
        );

        return new MiniLayoutWorld(
            new LocationLayoutInput(
                [district.Location, .. result.Locations],
                [.. district.Seats, .. result.Props],
                [result.Building],
                [.. result.Rooms],
                [.. result.LocationConnectors],
                [state]
            )
        );
    }

    internal static IReadOnlyList<IReadOnlyList<Guid>> HouseholdBedroomGroups(
        IReadOnlyList<Guid> memberIds
    ) =>
        memberIds.Count == 1
            ?
            [
                [memberIds[0]],
            ]
            :
            [
                [memberIds[0], memberIds[1]],
                .. memberIds.Skip(2).Select(id => (IReadOnlyList<Guid>)[id]),
            ];

    private static State MakeState(Guid worldId, Point center) =>
        new() { WorldId = worldId, Center = center };

    private static Location MakeWilderness(Guid worldId, Guid stateId) =>
        new()
        {
            WorldId = worldId,
            StateId = stateId,
            Kind = LocationKind.Wilderness,
        };

    private static LocationConnector MakeConnector(
        Guid worldId,
        Location origin,
        Location destination,
        string name
    ) =>
        new()
        {
            WorldId = worldId,
            OriginLocationId = origin.Id,
            DestinationLocationId = destination.Id,
            Name = name,
            DestinationLabel = name,
        };

    internal sealed record MiniLayoutWorld(LocationLayoutInput Input)
    {
        internal Location LocationById(Guid id) => Input.Locations.Single(l => l.Id == id);

        internal LocationConnector EntranceConnector(Building building) =>
            Input.Connectors.Single(connector =>
                connector.OriginLocationId == building.ExteriorLocationId
                && Input.Rooms.Any(room =>
                    room.LocationId == connector.DestinationLocationId
                    && room.BuildingId == building.Id
                )
            );

        internal IReadOnlyList<double> Snapshot() =>
            Input
                .Locations.SelectMany(location => new[] { location.Width, location.Depth })
                .Concat(
                    Input.Props.SelectMany(prop => new[] { prop.X, prop.Y, prop.Angle, prop.Width })
                )
                .Concat(Input.Buildings.SelectMany(building => new[] { building.X, building.Y }))
                .Concat(
                    Input.Connectors.SelectMany(connector =>
                        new[]
                        {
                            connector.ExitX,
                            connector.ExitY,
                            connector.ArrivalX,
                            connector.ArrivalY,
                        }
                    )
                )
                .ToArray();
    }
}
