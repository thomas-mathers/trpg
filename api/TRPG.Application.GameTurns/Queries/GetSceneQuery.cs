using TRPG.Application.Common.Queries;
using TRPG.Application.CreatureFormulas;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Creatures.Results;
using TRPG.Application.Factions.Queries;
using TRPG.Application.GameTurns.Results;
using TRPG.Application.Props.Queries;
using TRPG.Application.Quests.Queries;
using TRPG.Application.Reputations.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Application.Worlds.Results;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns.Queries;

public class GetSceneQuery
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required InGameDate CurrentDate { get; init; }
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
        GetConnectorsByLocationIdQuery,
        IReadOnlyCollection<LocationConnector>
    > getConnectorsByLocationId,
    IQueryHandler<
        GetBuildingsByLocationQuery,
        IReadOnlyCollection<Building>
    > getAllBuildingsByLocation,
    IQueryHandler<GetNearbyCreaturesQuery, IReadOnlyCollection<CreatureResult>> getNearbyCreatures,
    IQueryHandler<
        GetEffectiveReputationsQuery,
        IReadOnlyDictionary<Guid, int>
    > getEffectiveReputations,
    IQueryHandler<
        GetQuestMarkersForGiversQuery,
        IReadOnlyDictionary<Guid, QuestMarker>
    > getQuestMarkersForGivers,
    IQueryHandler<
        GetTotalCharacterXpFromSkillsQuery,
        IReadOnlyDictionary<Guid, int>
    > getTotalCharacterXpFromSkills,
    IQueryHandler<GetLocationsByIdsQuery, IReadOnlyDictionary<Guid, Location>> getLocationsByIds,
    IQueryHandler<GetRoomsByIdsQuery, IReadOnlyDictionary<Guid, Room>> getRoomsByIds,
    IQueryHandler<GetBuildingsByIdsQuery, IReadOnlyDictionary<Guid, Building>> getBuildingsByIds,
    IQueryHandler<GetDistrictsByIdsQuery, IReadOnlyDictionary<Guid, District>> getDistrictsByIds,
    IQueryHandler<
        GetDoorConnectorsByConnectorIdsQuery,
        IReadOnlyDictionary<Guid, DoorConnector>
    > getDoorConnectorsByConnectorIds,
    IQueryHandler<
        GetFactionIdsByCreatureIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>
    > getFactionIdsByCreatureIds,
    IQueryHandler<GetFactionsByIdsQuery, IReadOnlyDictionary<Guid, Faction>> getFactionsByIds,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<
        GetTradeWorkstationIdsByOccupantIdsQuery,
        IReadOnlyDictionary<Guid, Guid?>
    > getTradeWorkstationIdsByOccupantIds
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

        var state = await getStateById.Handle(
            new GetStateByIdQuery { Id = player.StateId },
            cancellationToken
        );
        var cityInfo = await BuildCityInfo(player, cancellationToken);
        var districtInfo = await BuildDistrictInfo(player, cancellationToken);

        var exitInfos = await BuildExitInfos(
            await getConnectorsByLocationId.Handle(
                new GetConnectorsByLocationIdQuery { LocationId = player.LocationId },
                cancellationToken
            ),
            player.RoomId != null,
            cancellationToken
        );
        var nearbyPeople = await BuildNearbyPeopleInfos(query, nearby, cancellationToken);

        var details =
            player.RoomId != null
                ? await BuildIndoorScene(player, cancellationToken)
                : await BuildOutdoorScene(player, state, cancellationToken);
        var playerCreatureInfo = await BuildPlayerCreatureInfo(query, player, cancellationToken);

        return new SceneResult(
            query.WorldId,
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
            details.NearbyBuildings
        );
    }

    private async Task<SceneCreatureInfo> BuildPlayerCreatureInfo(
        GetSceneQuery query,
        CreatureResult player,
        CancellationToken cancellationToken
    )
    {
        var playerXpTotals = await getTotalCharacterXpFromSkills.Handle(
            new GetTotalCharacterXpFromSkillsQuery { CreatureIds = [query.PlayerId] },
            cancellationToken
        );
        var totalCharacterXp = playerXpTotals.GetValueOrDefault(query.PlayerId, 0);

        return BuildSceneCreatureInfo(
            player,
            query.CurrentDate.Year,
            factionNames: [],
            state: null,
            reputation: null,
            totalCharacterXp
        );
    }

    private static SceneCreatureInfo BuildSceneCreatureInfo(
        CreatureResult creature,
        int currentYear,
        IReadOnlyCollection<string> factionNames,
        CreatureState? state,
        int? reputation,
        int totalCharacterXp,
        Guid? tradeWorkstationId = null,
        QuestMarker? questMarker = null
    )
    {
        var experienceProgress = SkillFormulas.GetExperienceProgress(
            creature.Level,
            totalCharacterXp
        );

        return new SceneCreatureInfo(
            creature.Id,
            creature.Name,
            creature.CreatureType,
            creature.Gender,
            creature.Profession,
            creature.Level,
            currentYear - creature.BirthYear,
            factionNames,
            state,
            reputation,
            creature.Gold,
            creature.CurrentHp,
            creature.MaximumHp,
            creature.CurrentAp,
            creature.MaximumAp,
            creature.CurrentMp,
            creature.MaximumMp,
            experienceProgress.Current,
            experienceProgress.ToNextLevel,
            creature.Strength,
            creature.Dexterity,
            creature.Intelligence,
            creature.Endurance,
            creature.Stamina,
            creature.Mana,
            creature.Defense,
            creature.MovementSpeed,
            creature.PhysicalResistance,
            creature.FireResistance,
            creature.IceResistance,
            creature.LightningResistance,
            creature.PoisonResistance,
            creature.MagicResistance,
            tradeWorkstationId,
            questMarker
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
        var buildingInfo = new SceneBuildingInfo(
            roomResult.BuildingName,
            roomResult.BuildingType,
            ownerName,
            faction?.Name,
            faction?.Description
        );
        var roomInfo = new SceneRoomInfo(
            roomResult.RoomName,
            roomResult.RoomDescription,
            roomResult.RoomFloorNumber
        );
        var nearbyProps = props
            .Select(p => new ScenePropInfo(p.Id, p.Name, p.Description, GetPropType(p)))
            .ToArray();

        return new SceneLocationData(buildingInfo, roomInfo, null, nearbyProps, []);
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
            .Select(b => new SceneNearbyBuildingInfo(b.Id, b.Name, b.BuildingType))
            .ToArray();

        return new SceneLocationData(null, null, state?.Description, [], nearbyBuildings);
    }

    private async Task<IReadOnlyCollection<SceneCreatureInfo>> BuildNearbyPeopleInfos(
        GetSceneQuery query,
        IReadOnlyCollection<CreatureResult> nearby,
        CancellationToken cancellationToken
    )
    {
        if (nearby.Count == 0)
        {
            return [];
        }

        var nearbyCreatureIds = nearby.Select(x => x.Id).ToArray();
        var factionIdsByCreature = await getFactionIdsByCreatureIds.Handle(
            new GetFactionIdsByCreatureIdsQuery { CreatureIds = nearbyCreatureIds },
            cancellationToken
        );
        var allFactionIds = factionIdsByCreature.Values.SelectMany(ids => ids).Distinct().ToArray();
        var factionsById = await getFactionsByIds.Handle(
            new GetFactionsByIdsQuery { Ids = allFactionIds },
            cancellationToken
        );

        var factionNamesByCreature = factionIdsByCreature.ToDictionary(
            kv => kv.Key,
            kv =>
                (IReadOnlyList<string>)
                    kv
                        .Value.Where(id =>
                            factionsById.TryGetValue(id, out var f) && !f.IsCityFaction
                        )
                        .Select(id => factionsById[id].Name)
                        .ToArray()
        );

        var reputationByCreature = await getEffectiveReputations.Handle(
            new GetEffectiveReputationsQuery
            {
                ObserverCreatureId = query.PlayerId,
                TargetCreatureIds = nearbyCreatureIds,
                FactionIdsByCreature = factionIdsByCreature,
            },
            cancellationToken
        );
        var tradeWorkstationIdsByCreature = await getTradeWorkstationIdsByOccupantIds.Handle(
            new GetTradeWorkstationIdsByOccupantIdsQuery { OccupantIds = nearbyCreatureIds },
            cancellationToken
        );
        var questMarkersByGiver = await getQuestMarkersForGivers.Handle(
            new GetQuestMarkersForGiversQuery
            {
                PlayerId = query.PlayerId,
                WorldId = query.WorldId,
                GiverIds = nearbyCreatureIds,
            },
            cancellationToken
        );

        return nearby
            .Select(x =>
                BuildSceneCreatureInfo(
                    x,
                    query.CurrentDate.Year,
                    factionNames: factionNamesByCreature.GetValueOrDefault(x.Id, []),
                    state: x.State,
                    reputation: reputationByCreature.GetValueOrDefault(x.Id, 0),
                    totalCharacterXp: 0,
                    tradeWorkstationId: tradeWorkstationIdsByCreature.GetValueOrDefault(x.Id),
                    questMarker: questMarkersByGiver.TryGetValue(x.Id, out var marker)
                        ? marker
                        : null
                )
            )
            .ToArray();
    }

    private async Task<IReadOnlyCollection<SceneExitInfo>> BuildExitInfos(
        IReadOnlyCollection<LocationConnector> connectors,
        bool sourceIsRoom,
        CancellationToken cancellationToken
    )
    {
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

        return connectors
            // Outdoors, a building's front door duplicates its NearbyBuildings entry.
            .Where(connector =>
                sourceIsRoom
                || destinations.GetValueOrDefault(connector.DestinationLocationId)?.Kind
                    != LocationKind.Room
            )
            .Select(connector => new SceneExitInfo(
                connector.Description,
                ToExitDestination(
                    connector,
                    destinations.GetValueOrDefault(connector.DestinationLocationId),
                    districts,
                    rooms,
                    buildings,
                    sourceIsRoom
                ),
                lockedConnectorIds.Contains(connector.Id)
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
            ? new SceneRoomExitDestination(connector.DestinationLabel, building.BuildingType)
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
            Trigger => "Trigger",
            _ => prop.GetType().Name,
        };
    }
}
