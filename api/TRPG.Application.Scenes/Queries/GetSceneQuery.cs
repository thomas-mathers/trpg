using Microsoft.Extensions.Options;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.CreatureFormulas;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Creatures.Results;
using TRPG.Application.Factions.Queries;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Knowledge.Queries;
using TRPG.Application.Props.Queries;
using TRPG.Application.Quests.Queries;
using TRPG.Application.Reputations.Queries;
using TRPG.Application.Scenes.Boundaries;
using TRPG.Application.Scenes.Navigation;
using TRPG.Application.Scenes.Neighbors;
using TRPG.Application.Scenes.Results;
using TRPG.Application.Scenes.Roads;
using TRPG.Application.Weather.Queries;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Application.Worlds.Queries;
using TRPG.Application.Worlds.Results;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Queries;

public class GetSceneQuery
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required InGameDate CurrentDate { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal record SceneLocationData(
    SceneBuildingInfo? Building,
    SceneRoomInfo? Room,
    string? RegionDescription,
    IReadOnlyCollection<ScenePropInfo> NearbyProps,
    IReadOnlyCollection<SceneNearbyBuildingInfo> NearbyBuildings
);

internal class GetSceneQueryHandler(
    IQueryHandler<GetStateByIdQuery, State?> getStateById,
    IQueryHandler<GetCityByIdQuery, City?> getCityById,
    IQueryHandler<GetCityByStateIdQuery, City?> getCityByStateId,
    IQueryHandler<GetDistrictByIdQuery, District?> getDistrictById,
    IQueryHandler<GetRoomQuery, RoomResult?> getRoom,
    IQueryHandler<GetPropsByLocationIdQuery, IReadOnlyCollection<Prop>> getAllPropsByLocationId,
    IQueryHandler<
        GetPlacedConnectorsByLocationIdQuery,
        IReadOnlyCollection<PlacedConnector>
    > getConnectorsByLocationId,
    IQueryHandler<
        GetBuildingsByLocationQuery,
        IReadOnlyCollection<Building>
    > getAllBuildingsByLocation,
    IQueryHandler<GetNearbyCreaturesQuery, IReadOnlyCollection<CreatureResult>> getNearbyCreatures,
    IQueryHandler<
        GetTotalCharacterXpFromSkillsQuery,
        IReadOnlyDictionary<Guid, int>
    > getTotalCharacterXpFromSkills,
    IQueryHandler<GetLocationsByIdsQuery, IReadOnlyDictionary<Guid, Location>> getLocationsByIds,
    IQueryHandler<GetRoomsByIdsQuery, IReadOnlyDictionary<Guid, Room>> getRoomsByIds,
    IQueryHandler<GetVisitedRoomLocationIdsQuery, IReadOnlySet<Guid>> getVisitedRoomLocationIds,
    IQueryHandler<GetKnownTrapIdsQuery, IReadOnlySet<Guid>> getKnownTrapIds,
    IQueryHandler<GetHiddenQuestTriggerIdsQuery, IReadOnlySet<Guid>> getHiddenQuestTriggerIds,
    IQueryHandler<GetBuildingPremiseQuery, string?> getBuildingPremise,
    IQueryHandler<GetBuildingsByIdsQuery, IReadOnlyDictionary<Guid, Building>> getBuildingsByIds,
    IQueryHandler<GetDistrictsByIdsQuery, IReadOnlyDictionary<Guid, District>> getDistrictsByIds,
    IQueryHandler<
        GetDoorConnectorsByConnectorIdsQuery,
        IReadOnlyDictionary<Guid, DoorConnector>
    > getDoorConnectorsByConnectorIds,
    IQueryHandler<GetFactionsByIdsQuery, IReadOnlyDictionary<Guid, Faction>> getFactionsByIds,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<
        GetSeatOccupantsByIdsQuery,
        IReadOnlyDictionary<Guid, Guid?>
    > getSeatOccupantsByIds,
    IQueryHandler<GetWeatherByStateIdQuery, WeatherCondition?> getWeatherByStateId,
    IQueryHandler<GetSceneNeighborsQuery, IReadOnlyCollection<NeighborDistrict>> getSceneNeighbors,
    IQueryHandler<GetPointNetworkByLocationIdQuery, LocationPointNetwork> getRoadNetwork,
    IQueryHandler<
        GetEquippedItemsByOwnersQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<Item>>
    > getEquippedItemsByOwners,
    SceneCreatureInfoBuilder creatureInfoBuilder
) : IQueryHandler<GetSceneQuery, SceneResult>
{
    public async Task<SceneResult> Handle(
        GetSceneQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var creaturesHere = await getNearbyCreatures.Handle(
            new GetNearbyCreaturesQuery { PlayerId = query.PlayerId },
            cancellationToken
        );
        var player = creaturesHere.Single(c => c.Id == query.PlayerId);
        var nearby = creaturesHere.Where(c => c.Id != query.PlayerId).ToArray();
        var equippedItemsByCreature = await getEquippedItemsByOwners.Handle(
            new GetEquippedItemsByOwnersQuery
            {
                CreatureIds = creaturesHere.Select(creature => creature.Id).ToArray(),
            },
            cancellationToken
        );

        var state = await getStateById.Handle(
            new GetStateByIdQuery { Id = player.StateId },
            cancellationToken
        );
        var cityInfo = await BuildCityInfo(player, cancellationToken);
        var districtInfo = await BuildDistrictInfo(player, cancellationToken);

        var connectors = await getConnectorsByLocationId.Handle(
            new GetPlacedConnectorsByLocationIdQuery { LocationId = player.LocationId },
            cancellationToken
        );
        var exitInfos = await BuildExitInfos(
            connectors,
            player.RoomId != null,
            player,
            cancellationToken
        );
        var nearbyPeople = await creatureInfoBuilder.BuildNearbyPeopleInfos(
            query,
            player.LocationId,
            nearby,
            equippedItemsByCreature,
            cancellationToken
        );

        var details =
            player.RoomId != null
                ? await BuildIndoorScene(query.WorldId, player, cancellationToken)
                : await BuildOutdoorScene(query.WorldId, player, state, cancellationToken);
        var playerCreatureInfo = await BuildPlayerCreatureInfo(
            query,
            player,
            equippedItemsByCreature.GetValueOrDefault(player.Id, []).ToVisualEquipment(),
            cancellationToken
        );
        var weather =
            player.RoomId == null
                ? await getWeatherByStateId.Handle(
                    new GetWeatherByStateIdQuery { StateId = player.StateId },
                    cancellationToken
                )
                : null;
        var nearbyCaravans = await BuildNearbyCaravans(
            query.WorldId,
            query.PlayerId,
            player.LocationId,
            query.GameTime,
            weather,
            cancellationToken
        );
        var size = await GetSceneSize(player.LocationId, cancellationToken);
        var isDistrictOutdoors = player.RoomId == null && districtInfo != null;
        var neighbors = isDistrictOutdoors
            ? await getSceneNeighbors.Handle(
                new GetSceneNeighborsQuery
                {
                    LocationId = player.LocationId,
                    Size = size,
                    Exits = exitInfos,
                    Buildings = details.NearbyBuildings,
                },
                cancellationToken
            )
            : null;
        var pointNetwork = await getRoadNetwork.Handle(
            new GetPointNetworkByLocationIdQuery { LocationId = player.LocationId },
            cancellationToken
        );

        return new SceneResult(
            query.WorldId,
            player.LocationId,
            new SceneDateInfo(
                query.CurrentDate.Year,
                query.CurrentDate.MonthName,
                query.CurrentDate.Day,
                query.CurrentDate.WeekdayName,
                query.CurrentDate.Hour
            ),
            new SceneStateInfo(state!.Name, details.RegionDescription),
            cityInfo,
            districtInfo,
            details.Building,
            details.Room,
            playerCreatureInfo,
            exitInfos,
            details.NearbyProps,
            nearbyPeople,
            details.NearbyBuildings,
            weather,
            nearbyCaravans,
            size,
            isDistrictOutdoors
                ? SceneBoundaryResolver.Resolve(size, exitInfos, neighbors ?? [])
                : null,
            isDistrictOutdoors ? SceneRoadMapper.ToRoads(pointNetwork) : null,
            neighbors,
            PointNetworkMapper.ToTravelNetwork(pointNetwork)
        );
    }

    private async Task<Footprint> GetSceneSize(Guid locationId, CancellationToken cancellationToken)
    {
        var locations = await getLocationsByIds.Handle(
            new GetLocationsByIdsQuery { Ids = [locationId] },
            cancellationToken
        );
        var location = locations[locationId];

        return new Footprint(location.Width, location.Depth);
    }

    private static Task<IReadOnlyCollection<SceneCaravanInfo>> BuildNearbyCaravans(
        Guid worldId,
        Guid playerId,
        Guid playerLocationId,
        GameInstant gameTime,
        WeatherCondition? weather,
        CancellationToken cancellationToken
    ) => Task.FromResult<IReadOnlyCollection<SceneCaravanInfo>>([]);

    private async Task<SceneCreatureInfo> BuildPlayerCreatureInfo(
        GetSceneQuery query,
        CreatureResult player,
        IReadOnlyCollection<SceneEquipmentVisual> equipment,
        CancellationToken cancellationToken
    )
    {
        var playerXpTotals = await getTotalCharacterXpFromSkills.Handle(
            new GetTotalCharacterXpFromSkillsQuery { CreatureIds = [query.PlayerId] },
            cancellationToken
        );
        var totalCharacterXp = playerXpTotals.GetValueOrDefault(query.PlayerId, 0);

        return SceneCreatureInfoBuilder.BuildSceneCreatureInfo(
            player,
            query.CurrentDate.Year,
            factionNames: [],
            player.Condition,
            player.Activity,
            player.Posture,
            player.Movement,
            reputation: null,
            totalCharacterXp,
            placement: new Placement(player.X, player.Y, player.Angle),
            equipment: equipment
        );
    }

    private async Task<SceneCityInfo?> BuildCityInfo(
        CreatureResult player,
        CancellationToken cancellationToken
    )
    {
        var city =
            player.CityId != null
                ? await getCityById.Handle(
                    new GetCityByIdQuery { Id = player.CityId.Value },
                    cancellationToken
                )
                : await getCityByStateId.Handle(
                    new GetCityByStateIdQuery { StateId = player.StateId },
                    cancellationToken
                );
        return city != null ? new SceneCityInfo(city.Name, city.Description) : null;
    }

    private async Task<SceneDistrictInfo?> BuildDistrictInfo(
        CreatureResult player,
        CancellationToken cancellationToken
    )
    {
        if (player.DistrictId == null)
        {
            return null;
        }

        var district = await getDistrictById.Handle(
            new GetDistrictByIdQuery { Id = player.DistrictId.Value },
            cancellationToken
        );
        return district != null
            ? new SceneDistrictInfo(district.Id, district.Name, district.DistrictType)
            : null;
    }

    private async Task<SceneLocationData> BuildIndoorScene(
        Guid worldId,
        CreatureResult player,
        CancellationToken cancellationToken
    )
    {
        var roomResult = await getRoom.Handle(
            new GetRoomQuery { RoomId = player.RoomId!.Value },
            cancellationToken
        );

        var props = await getAllPropsByLocationId.Handle(
            new GetPropsByLocationIdQuery { LocationId = player.LocationId },
            cancellationToken
        );

        var ownerName = roomResult!.OwnerId is { } ownerId
            ? (
                await getCreatureById.Handle(
                    new GetCreatureByIdQuery { Id = ownerId },
                    cancellationToken
                )
            )?.Name
            : null;
        var faction = await GetFaction(roomResult.FactionId, cancellationToken);

        // Only dungeons have one, so nothing else pays for the lookup.
        var premise = BuildingTypes.Dungeon.Contains(roomResult.BuildingType)
            ? await getBuildingPremise.Handle(
                new GetBuildingPremiseQuery { BuildingId = roomResult.BuildingId },
                cancellationToken
            )
            : null;

        var buildingInfo = new SceneBuildingInfo(
            roomResult.BuildingName,
            roomResult.BuildingType,
            ownerName,
            faction?.Name,
            faction?.Description,
            premise
        );
        var roomInfo = new SceneRoomInfo(
            roomResult.RoomName,
            roomResult.RoomDescription,
            roomResult.RoomFloorNumber
        );
        var visibleProps = await ExcludeHiddenProps(props, worldId, player.Id, cancellationToken);
        var nearbyProps = await BuildNearbyProps(visibleProps, player.Id, cancellationToken);

        return new SceneLocationData(buildingInfo, roomInfo, null, nearbyProps, []);
    }

    private async Task<IReadOnlyCollection<Prop>> ExcludeHiddenProps(
        IReadOnlyCollection<Prop> props,
        Guid worldId,
        Guid playerId,
        CancellationToken cancellationToken
    )
    {
        var withoutTraps = await ExcludeUndiscoveredTraps(props, playerId, cancellationToken);
        return await ExcludeHiddenQuestTriggers(withoutTraps, worldId, playerId, cancellationToken);
    }

    private async Task<IReadOnlyCollection<Prop>> ExcludeHiddenQuestTriggers(
        IReadOnlyCollection<Prop> props,
        Guid worldId,
        Guid playerId,
        CancellationToken cancellationToken
    )
    {
        var triggerIds = props.OfType<Trigger>().Select(trigger => trigger.Id).ToArray();
        if (triggerIds.Length == 0)
        {
            return props;
        }

        var hiddenTriggerIds = await getHiddenQuestTriggerIds.Handle(
            new GetHiddenQuestTriggerIdsQuery
            {
                WorldId = worldId,
                PlayerId = playerId,
                TriggerIds = triggerIds,
            },
            cancellationToken
        );

        return props.Where(prop => !hiddenTriggerIds.Contains(prop.Id)).ToArray();
    }

    // An undiscovered trap must never reach the narrator, or it warns the player about a hazard
    // they have no way of knowing exists.
    private async Task<IReadOnlyCollection<Prop>> ExcludeUndiscoveredTraps(
        IReadOnlyCollection<Prop> props,
        Guid playerId,
        CancellationToken cancellationToken
    )
    {
        var trapIds = props.OfType<Trap>().Select(trap => trap.Id).ToArray();
        if (trapIds.Length == 0)
        {
            return props;
        }

        var knownTrapIds = await getKnownTrapIds.Handle(
            new GetKnownTrapIdsQuery { CreatureId = playerId, TrapIds = trapIds },
            cancellationToken
        );

        return props.Where(p => p is not Trap trap || knownTrapIds.Contains(trap.Id)).ToArray();
    }

    private async Task<Faction?> GetFaction(Guid? factionId, CancellationToken cancellationToken)
    {
        if (factionId is { } id)
        {
            var factionsById = await getFactionsByIds.Handle(
                new GetFactionsByIdsQuery { Ids = [id] },
                cancellationToken
            );
            return factionsById.GetValueOrDefault(id);
        }

        return null;
    }

    private async Task<SceneLocationData> BuildOutdoorScene(
        Guid worldId,
        CreatureResult player,
        State? state,
        CancellationToken cancellationToken
    )
    {
        var buildings = await getAllBuildingsByLocation.Handle(
            new GetBuildingsByLocationQuery { LocationId = player.LocationId },
            cancellationToken
        );

        var nearbyBuildings = buildings
            .Select(b => new SceneNearbyBuildingInfo(
                b.Id,
                b.Name,
                b.BuildingType,
                new Placement(b.X, b.Y, b.Angle),
                new Footprint(b.Width, b.Depth),
                b.FloorCount
            ))
            .ToArray();

        var props = await getAllPropsByLocationId.Handle(
            new GetPropsByLocationIdQuery { LocationId = player.LocationId },
            cancellationToken
        );
        var visibleProps = await ExcludeHiddenProps(props, worldId, player.Id, cancellationToken);
        var nearbyProps = await BuildNearbyProps(visibleProps, player.Id, cancellationToken);

        return new SceneLocationData(null, null, state?.Description, nearbyProps, nearbyBuildings);
    }

    private async Task<IReadOnlyCollection<SceneExitInfo>> BuildExitInfos(
        IReadOnlyCollection<PlacedConnector> placedConnectors,
        bool sourceIsRoom,
        CreatureResult player,
        CancellationToken cancellationToken
    )
    {
        var connectors = placedConnectors.Select(placed => placed.Connector).ToArray();
        var destinationLocationIds = connectors
            .Select(connector => connector.DestinationLocationId)
            .ToArray();
        var destinations = await getLocationsByIds.Handle(
            new GetLocationsByIdsQuery { Ids = destinationLocationIds },
            cancellationToken
        );
        var districtIds = destinations
            .Values.Select(location => location.DistrictId)
            .OfType<Guid>()
            .ToArray();
        var districts = await getDistrictsByIds.Handle(
            new GetDistrictsByIdsQuery { Ids = districtIds },
            cancellationToken
        );
        var roomIds = destinations
            .Values.Select(location => location.RoomId)
            .OfType<Guid>()
            .ToArray();
        var rooms = await getRoomsByIds.Handle(
            new GetRoomsByIdsQuery { Ids = roomIds },
            cancellationToken
        );
        var buildingIds = rooms.Values.Select(room => room.BuildingId).ToArray();
        var buildings = await getBuildingsByIds.Handle(
            new GetBuildingsByIdsQuery { Ids = buildingIds },
            cancellationToken
        );
        var connectorIds = connectors.Select(connector => connector.Id).ToArray();
        var doorConnectorsByConnectorId = await getDoorConnectorsByConnectorIds.Handle(
            new GetDoorConnectorsByConnectorIdsQuery { ConnectorIds = connectorIds },
            cancellationToken
        );
        var lockedConnectorIds = doorConnectorsByConnectorId
            .Where(kv => kv.Value.IsLocked)
            .Select(kv => kv.Key)
            .ToArray();

        // Somewhere already stood in is somewhere the player can be told they have been, which is
        // what stops a dungeon turning into unintentional backtracking. Only rooms are tracked, so
        // outdoors this asks nothing.
        var roomDestinationIds = destinations
            .Where(destination => destination.Value.Kind == LocationKind.Room)
            .Select(destination => destination.Key)
            .ToArray();
        var visited = await getVisitedRoomLocationIds.Handle(
            new GetVisitedRoomLocationIdsQuery
            {
                CreatureId = player.Id,
                RoomLocationIds = roomDestinationIds,
            },
            cancellationToken
        );

        var placementByConnectorId = placedConnectors.ToDictionary(
            placed => placed.Connector.Id,
            placed => new Placement(placed.Exit.X, placed.Exit.Y, placed.Connector.ExitAngle)
        );

        return connectors
            .Select(connector => new SceneExitInfo(
                connector.Id,
                connector.Description,
                ToExitDestination(
                    connector,
                    destinations.GetValueOrDefault(connector.DestinationLocationId),
                    districts,
                    rooms,
                    buildings,
                    sourceIsRoom
                ),
                lockedConnectorIds.Contains(connector.Id),
                connector.Direction,
                visited.Contains(connector.DestinationLocationId),
                connector.DestinationLocationId == player.PreviousLocationId,
                connector.DestinationLocationId,
                placementByConnectorId[connector.Id],
                connector.StairDirection
            ))
            .ToArray();
    }

    private static SceneExitDestination ToExitDestination(
        LocationConnector connector,
        Location? location,
        IReadOnlyDictionary<Guid, District> districts,
        IReadOnlyDictionary<Guid, Room> rooms,
        IReadOnlyDictionary<Guid, Building> buildings,
        bool sourceIsRoom
    )
    {
        return location?.Kind switch
        {
            LocationKind.Room => ToRoomExitDestination(
                connector,
                location.RoomId!.Value,
                rooms,
                buildings,
                sourceIsRoom
            ),
            LocationKind.District => new SceneDistrictExitDestination(
                connector.DestinationLabel,
                districts[location.DistrictId!.Value].DistrictType
            ),
            _ => new SceneWildernessExitDestination(connector.DestinationLabel),
        };
    }

    private static SceneExitDestination ToRoomExitDestination(
        LocationConnector connector,
        Guid roomId,
        IReadOnlyDictionary<Guid, Room> rooms,
        IReadOnlyDictionary<Guid, Building> buildings,
        bool sourceIsRoom
    )
    {
        var building = buildings[rooms[roomId].BuildingId];
        return sourceIsRoom
            ? new SceneRoomExitDestination(
                connector.DestinationLabel,
                building.BuildingType,
                rooms[roomId].Role
            )
            : new SceneBuildingExitDestination(connector.DestinationLabel, building.BuildingType);
    }

    private static string GetPropType(Prop prop)
    {
        return prop switch
        {
            Workstation w => w.WorkstationType.ToString(),
            Bed => "Bed",
            Seat => "Seat",
            Container => "Container",
            Trap => "Trap",
            Trigger => "Trigger",
            Sign => "Sign",
            Furniture => ScenePropInfo.FurnitureType,
            _ => prop.GetType().Name,
        };
    }

    private async Task<IReadOnlyCollection<ScenePropInfo>> BuildNearbyProps(
        IReadOnlyCollection<Prop> props,
        Guid playerId,
        CancellationToken cancellationToken
    )
    {
        var seatIds = props.OfType<Seat>().Select(seat => seat.Id).ToArray();
        var occupantsBySeatId = await getSeatOccupantsByIds.Handle(
            new GetSeatOccupantsByIdsQuery { SeatIds = seatIds },
            cancellationToken
        );

        return props
            .Select(prop =>
            {
                var occupantId = occupantsBySeatId.GetValueOrDefault(prop.Id);
                return new ScenePropInfo(
                    prop.Id,
                    prop.Name,
                    prop.Description,
                    GetPropType(prop),
                    IsOccupied: occupantId != null,
                    IsOccupiedByPlayer: occupantId == playerId,
                    Model: PropModelResolver.Resolve(prop),
                    Placement: new Placement(prop.X, prop.Y, prop.Angle),
                    Footprint: new Footprint(prop.Width, prop.Depth)
                );
            })
            .ToArray();
    }
}
