using Microsoft.Extensions.Options;
using TRPG.Application.Caravans;
using TRPG.Application.Caravans.Queries;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.CreatureFormulas;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Creatures.Results;
using TRPG.Application.Factions.Queries;
using TRPG.Application.GameTurns.Results;
using TRPG.Application.Knowledge.Queries;
using TRPG.Application.LocationSimulation.Queries;
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
    public required TimeSpan Playtime { get; init; }
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
    IQueryHandler<GetQuestMarkersForCreaturesQuery, QuestMarkersResult> getQuestMarkersForCreatures,
    IQueryHandler<
        GetTotalCharacterXpFromSkillsQuery,
        IReadOnlyDictionary<Guid, int>
    > getTotalCharacterXpFromSkills,
    IQueryHandler<GetLocationsByIdsQuery, IReadOnlyDictionary<Guid, Location>> getLocationsByIds,
    IQueryHandler<GetRoomsByIdsQuery, IReadOnlyDictionary<Guid, Room>> getRoomsByIds,
    IQueryHandler<GetVisitedRoomLocationIdsQuery, IReadOnlySet<Guid>> getVisitedRoomLocationIds,
    IQueryHandler<GetKnownTrapIdsQuery, IReadOnlySet<Guid>> getKnownTrapIds,
    IQueryHandler<GetBuildingPremiseQuery, string?> getBuildingPremise,
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
    > getTradeWorkstationIdsByOccupantIds,
    IQueryHandler<GetWeatherByStateIdQuery, WeatherCondition?> getWeatherByStateId,
    IQueryHandler<
        GetCaravansByLocationIdQuery,
        IReadOnlyList<CaravanSummary>
    > getCaravansByLocationId,
    IQueryHandler<ResolveCaravanPositionQuery, CaravanPosition?> resolveCaravanPosition,
    IQueryHandler<GetCaravanTicketQuery, CaravanTicket?> getCaravanTicket,
    IQueryHandler<GetCitiesByIdsQuery, IReadOnlyDictionary<Guid, City>> getCitiesByIds,
    IOptionsSnapshot<CaravanOptions> caravanOptions
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
            player,
            cancellationToken
        );
        var nearbyPeople = await BuildNearbyPeopleInfos(query, nearby, cancellationToken);

        var details =
            player.RoomId != null
                ? await BuildIndoorScene(player, cancellationToken)
                : await BuildOutdoorScene(player, state, cancellationToken);
        var playerCreatureInfo = await BuildPlayerCreatureInfo(query, player, cancellationToken);
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
            query.Playtime,
            cancellationToken
        );

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
            details.NearbyBuildings,
            weather,
            nearbyCaravans
        );
    }

    private async Task<IReadOnlyCollection<SceneCaravanInfo>> BuildNearbyCaravans(
        Guid worldId,
        Guid playerId,
        Guid playerLocationId,
        TimeSpan playtime,
        CancellationToken cancellationToken
    )
    {
        var lingeringCaravans = await ResolveLingeringCaravans(
            worldId,
            playerId,
            playerLocationId,
            playtime,
            cancellationToken
        );
        if (lingeringCaravans.Count == 0)
        {
            return [];
        }

        var destinationLocationIds = lingeringCaravans
            .SelectMany(entry => entry.Caravan.Stops.Select(stop => stop.LocationId))
            .Distinct()
            .ToArray();
        var namesByLocationId = await ResolveCaravanStopNames(
            destinationLocationIds,
            cancellationToken
        );

        var result = new List<SceneCaravanInfo>();
        foreach (var (caravan, position) in lingeringCaravans)
        {
            var ticket = await getCaravanTicket.Handle(
                new GetCaravanTicketQuery { CreatureId = playerId, CaravanId = caravan.CaravanId },
                cancellationToken
            );

            var destinations = BuildCaravanDestinations(
                caravan,
                position.StopIndex,
                ticket,
                namesByLocationId
            );
            result.Add(
                new SceneCaravanInfo(
                    caravan.CaravanId,
                    caravan.RouteName,
                    caravan.TicketFeeGold,
                    (int)Math.Ceiling(position.HoursUntilDeparture * 60),
                    destinations
                )
            );
        }

        return result;
    }

    private async Task<
        List<(CaravanSummary Caravan, CaravanPosition.Lingering Position)>
    > ResolveLingeringCaravans(
        Guid worldId,
        Guid playerId,
        Guid playerLocationId,
        TimeSpan playtime,
        CancellationToken cancellationToken
    )
    {
        var caravans = await getCaravansByLocationId.Handle(
            new GetCaravansByLocationIdQuery { WorldId = worldId, LocationId = playerLocationId },
            cancellationToken
        );

        var lingering = new List<(CaravanSummary, CaravanPosition.Lingering)>();
        foreach (var caravan in caravans)
        {
            var position = await resolveCaravanPosition.Handle(
                new ResolveCaravanPositionQuery
                {
                    CaravanId = caravan.CaravanId,
                    Playtime = playtime,
                },
                cancellationToken
            );
            if (
                position is CaravanPosition.Lingering atThisStop
                && atThisStop.LocationId == playerLocationId
            )
            {
                lingering.Add((caravan, atThisStop));
                continue;
            }

            // A ticketed player standing right here shouldn't see the caravan vanish just because
            // ordinary narration-time overhead (every narrated turn advances playtime a little)
            // nudged its live position past the strict window — BoardCaravanCommand honors the
            // same ticket regardless of this drift, so the scene has to agree.
            var ticket = await getCaravanTicket.Handle(
                new GetCaravanTicketQuery { CreatureId = playerId, CaravanId = caravan.CaravanId },
                cancellationToken
            );
            if (ticket != null && ticket.OriginStopLocationId == playerLocationId)
            {
                var stopIndex = caravan
                    .Stops.ToList()
                    .FindIndex(stop => stop.LocationId == playerLocationId);
                lingering.Add(
                    (
                        caravan,
                        new CaravanPosition.Lingering(
                            playerLocationId,
                            stopIndex,
                            HoursUntilDeparture: 0
                        )
                    )
                );
            }
        }

        return lingering;
    }

    private async Task<IReadOnlyDictionary<Guid, string>> ResolveCaravanStopNames(
        IReadOnlyCollection<Guid> locationIds,
        CancellationToken cancellationToken
    )
    {
        var locations = await getLocationsByIds.Handle(
            new GetLocationsByIdsQuery { Ids = locationIds },
            cancellationToken
        );
        var districtIds = locations
            .Values.Select(location => location.DistrictId)
            .OfType<Guid>()
            .ToArray();
        var districts = await getDistrictsByIds.Handle(
            new GetDistrictsByIdsQuery { Ids = districtIds },
            cancellationToken
        );
        var cityIds = districts.Values.Select(district => district.CityId).Distinct().ToArray();
        var cities = await getCitiesByIds.Handle(
            new GetCitiesByIdsQuery { Ids = cityIds },
            cancellationToken
        );

        // A caravan stop's location is a CityEntrance district — its own name is a generic gate
        // name reused across many cities (e.g. "The Outer Gate"), so the destination the player
        // actually cares about is the city it belongs to, not the district.
        return locationIds.ToDictionary(
            id => id,
            id =>
                locations.TryGetValue(id, out var location)
                && location.DistrictId is { } districtId
                && districts.TryGetValue(districtId, out var district)
                    ? cities.GetValueOrDefault(district.CityId)?.Name ?? "Unknown"
                    : "Unknown"
        );
    }

    private IReadOnlyCollection<SceneCaravanDestination> BuildCaravanDestinations(
        CaravanSummary caravan,
        int currentStopIndex,
        CaravanTicket? ticket,
        IReadOnlyDictionary<Guid, string> namesByLocationId
    )
    {
        var destinations = new List<SceneCaravanDestination>();
        for (var offset = 1; offset < caravan.Stops.Count; offset++)
        {
            var destinationIndex = (currentStopIndex + offset) % caravan.Stops.Count;
            var destinationLocationId = caravan.Stops[destinationIndex].LocationId;
            var travelTimeHours = CaravanCycle.HoursBetween(
                caravan.Stops,
                caravan.LingerHours,
                caravanOptions.Value.SpeedUnitsPerHour,
                currentStopIndex,
                destinationIndex
            );

            destinations.Add(
                new SceneCaravanDestination(
                    destinationLocationId,
                    namesByLocationId.GetValueOrDefault(destinationLocationId, "Unknown"),
                    (int)Math.Ceiling(travelTimeHours),
                    ticket?.DestinationLocationId == destinationLocationId
                )
            );
        }

        return destinations;
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
        IReadOnlyCollection<QuestMarkerEntry>? questMarkers = null,
        bool readyToDeliver = false
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
            creature.IsSneaking,
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
            questMarkers ?? [],
            readyToDeliver
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
        var visibleProps = await ExcludeUndiscoveredTraps(props, player.Id, cancellationToken);
        var nearbyProps = visibleProps
            .Select(p => new ScenePropInfo(p.Id, p.Name, p.Description, GetPropType(p)))
            .ToArray();

        return new SceneLocationData(buildingInfo, roomInfo, null, nearbyProps, []);
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
        var questMarkers = await getQuestMarkersForCreatures.Handle(
            new GetQuestMarkersForCreaturesQuery
            {
                PlayerId = query.PlayerId,
                WorldId = query.WorldId,
                CreatureIds = nearbyCreatureIds,
            },
            cancellationToken
        );

        var xpTotalsByCreature = await getTotalCharacterXpFromSkills.Handle(
            new GetTotalCharacterXpFromSkillsQuery { CreatureIds = nearbyCreatureIds },
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
                    totalCharacterXp: xpTotalsByCreature.GetValueOrDefault(x.Id, 0),
                    tradeWorkstationId: tradeWorkstationIdsByCreature.GetValueOrDefault(x.Id),
                    questMarkers: questMarkers.EntriesByCreatureId.GetValueOrDefault(x.Id, []),
                    readyToDeliver: questMarkers.ReadyToDeliverCreatureIds.Contains(x.Id)
                )
            )
            .ToArray();
    }

    private async Task<IReadOnlyCollection<SceneExitInfo>> BuildExitInfos(
        IReadOnlyCollection<LocationConnector> connectors,
        bool sourceIsRoom,
        CreatureResult player,
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
                lockedConnectorIds.Contains(connector.Id),
                connector.Direction,
                visited.Contains(connector.DestinationLocationId),
                connector.DestinationLocationId == player.PreviousLocationId
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
            _ => prop.GetType().Name,
        };
    }
}
