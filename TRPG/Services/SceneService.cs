using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

internal record SceneQuery(Guid WorldId, Guid PlayerId, InGameDate CurrentDate);

internal record SceneStateInfo(string Name, string? Description);

internal record SceneDistrictInfo(string Name, string Type, bool IsCurrent);

internal record SceneCityInfo(string Name, string? Description, IReadOnlyCollection<SceneDistrictInfo> Districts);

internal record SceneBuildingInfo(
    string Name,
    string Type,
    string? OwnerName,
    string? FactionName,
    string? FactionDescription);

internal record SceneExitInfo(string Description, string DestinationRoomName, bool IsLocked);

internal record SceneRoomInfo(
    string Name,
    string Description,
    int FloorNumber,
    IReadOnlyCollection<SceneExitInfo> Exits);

internal record ScenePlayerInfo(
    string Name, string CreatureType, string Gender, string Profession, int Level, int Gold, int Age);

internal record ScenePropInfo(string Name, string Description, string Type);

internal record SceneCreatureInfo(
    string Name,
    string CreatureType,
    string Gender,
    string Profession,
    int Level,
    int Age,
    IReadOnlyCollection<string> FactionNames,
    string State,
    int Reputation);

internal record SceneNearbyBuildingInfo(string Name, string Type);

internal record SceneResult(
    InGameDate CurrentDate,
    SceneStateInfo? State,
    SceneCityInfo? City,
    SceneBuildingInfo? Building,
    SceneRoomInfo? Room,
    ScenePlayerInfo Player,
    IReadOnlyCollection<ScenePropInfo> NearbyProps,
    IReadOnlyCollection<SceneCreatureInfo> NearbyPeople,
    IReadOnlyCollection<SceneNearbyBuildingInfo> NearbyBuildings
);

internal record SceneBootstrap(
    string PlayerName,
    Profession? Profession,
    int Level,
    int Gold,
    int BirthYear,
    Guid StateId,
    Guid? CityId,
    Guid? DistrictId,
    Guid? RoomId,
    string CreatureTypeName,
    string GenderName);

internal record SceneLocationDetails(
    SceneBuildingInfo? Building,
    SceneRoomInfo? Room,
    string? RegionDescription,
    IReadOnlyCollection<ScenePropInfo> NearbyProps,
    IReadOnlyCollection<SceneCreatureInfo> NearbyPeople,
    IReadOnlyCollection<SceneNearbyBuildingInfo> NearbyBuildings
);

internal class SceneService(
    TrpgDbContext context,
    LocationService locationService,
    BuildingService buildingService,
    CreatureService creatureService,
    ReputationService reputationService,
    ILogger<SceneService> logger
) {
    public async Task<SceneResult> GetScene(SceneQuery query, CancellationToken cancellationToken = default) {
        var stopwatch = Stopwatch.StartNew();

        var bootstrap = await GetBootstrap(query.PlayerId, cancellationToken);
        var state = await locationService.GetStateById(bootstrap.StateId, cancellationToken);
        var cityInfo = await BuildCityInfo(bootstrap, cancellationToken);

        var details = bootstrap.RoomId != null
            ? await BuildIndoorScene(query, bootstrap, cancellationToken)
            : await BuildOutdoorScene(query, bootstrap, state, cancellationToken);

        logger.LogInformation("[perf] GetScene ({Branch}) took {ElapsedMs}ms, {CreatureCount} nearby people",
            bootstrap.RoomId != null ? "indoor" : "outdoor", stopwatch.ElapsedMilliseconds,
            details.NearbyPeople.Count);

        return new SceneResult(
            query.CurrentDate,
            new SceneStateInfo(state!.Name, details.RegionDescription),
            cityInfo,
            details.Building,
            details.Room,
            new ScenePlayerInfo(bootstrap.PlayerName, bootstrap.CreatureTypeName, bootstrap.GenderName,
                bootstrap.Profession.ToString()!, bootstrap.Level, bootstrap.Gold,
                query.CurrentDate.Year - bootstrap.BirthYear),
            details.NearbyProps,
            details.NearbyPeople,
            details.NearbyBuildings
        );
    }

    private async Task<SceneBootstrap> GetBootstrap(Guid playerId, CancellationToken cancellationToken) {
        return await context.Creatures.AsNoTracking()
            .Where(p => p.Id == playerId)
            .Select(p => new SceneBootstrap(p.Name, p.Profession, p.Level, p.Gold, p.BirthYear, p.StateId, p.CityId,
                p.DistrictId, p.RoomId, p.CreatureType.ToString(), p.Gender.ToString()))
            .FirstAsync(cancellationToken);
    }

    private async Task<SceneCityInfo?> BuildCityInfo(SceneBootstrap bootstrap, CancellationToken cancellationToken) {
        if (bootstrap.CityId == null) {
            return null;
        }

        var city = await locationService.GetCityById(bootstrap.CityId.Value, cancellationToken);
        var districts = await locationService.GetAllDistrictsByCityId(bootstrap.CityId.Value, cancellationToken);
        var districtInfos = districts
            .Select(d => new SceneDistrictInfo(d.Name, d.DistrictType.ToString(), d.Id == bootstrap.DistrictId))
            .ToArray();
        return new SceneCityInfo(city!.Name, city.Description, districtInfos);
    }

    private async Task<SceneLocationDetails> BuildIndoorScene(SceneQuery query, SceneBootstrap bootstrap,
        CancellationToken cancellationToken) {
        var roomSummary = await buildingService.GetRoomSummary(bootstrap.RoomId!.Value, cancellationToken);

        var props = await buildingService.GetStaticPropsByRoomId(bootstrap.RoomId.Value, cancellationToken);
        var connectors = await buildingService.GetConnectorsByRoomId(bootstrap.RoomId.Value, cancellationToken);
        var exitInfos = await BuildExitInfos(connectors, cancellationToken);
        
        var nearbyPeople = await BuildNearbyPeople(query, bootstrap, cancellationToken);

        var buildingInfo = new SceneBuildingInfo(roomSummary!.BuildingName, roomSummary.BuildingType.ToString(),
            roomSummary.OwnerName, roomSummary.FactionName, roomSummary.FactionDescription);
        var roomInfo = new SceneRoomInfo(roomSummary.RoomName, roomSummary.RoomDescription,
            roomSummary.RoomFloorNumber, exitInfos);
        var nearbyProps = props.Select(p => new ScenePropInfo(p.Name, p.Description, GetPropType(p))).ToArray();

        return new SceneLocationDetails(buildingInfo, roomInfo, null, nearbyProps, nearbyPeople, []);
    }

    private async Task<SceneLocationDetails> BuildOutdoorScene(SceneQuery query, SceneBootstrap bootstrap,
        State? state, CancellationToken cancellationToken) {
        var buildings = await buildingService.GetAllByLocation(bootstrap.StateId, bootstrap.CityId,
            bootstrap.DistrictId, cancellationToken);
        var nearbyBuildings = buildings
            .Select(b => new SceneNearbyBuildingInfo(b.Name, b.BuildingType.ToString()))
            .ToArray();
        
        var nearbyPeople = await BuildNearbyPeople(query, bootstrap, cancellationToken);

        return new SceneLocationDetails(null, null, state?.Description, [], nearbyPeople, nearbyBuildings);
    }

    private async Task<IReadOnlyCollection<SceneCreatureInfo>> BuildNearbyPeople(
        SceneQuery query, 
        SceneBootstrap bootstrap,
        CancellationToken cancellationToken) {
        var creatureLocation = new CreatureLocation(query.WorldId, bootstrap.RoomId, bootstrap.StateId, bootstrap.DistrictId);
        var nearbyPeopleRaw = await creatureService.GetAllNearby(creatureLocation, query.PlayerId, cancellationToken);

        var nearbyCreatureIds = nearbyPeopleRaw.Select(x => x.Id).ToArray();
        var factionMembershipsByCreature = await (
                from fm in context.FactionMembers
                where nearbyCreatureIds.Contains(fm.CreatureId)
                join f in context.Factions on fm.FactionId equals f.Id
                select new { fm.CreatureId, fm.FactionId, f.Name }
            )
            .GroupBy(x => x.CreatureId)
            .ToDictionaryAsync(g => g.Key,
                g => new { FactionIds = g.Select(x => x.FactionId).ToArray(), FactionNames = g.Select(x => x.Name).ToArray() },
                cancellationToken);

        var factionNamesByCreature = factionMembershipsByCreature
            .ToDictionary(kv => kv.Key, kv => kv.Value.FactionNames);
        var factionIdsByCreature = factionMembershipsByCreature
            .ToDictionary(kv => kv.Key, kv => kv.Value.FactionIds);

        var reputationStopwatch = Stopwatch.StartNew();
        var reputationByCreature = await reputationService.GetEffectiveReputations(query.PlayerId, nearbyCreatureIds,
            factionIdsByCreature, cancellationToken);
        logger.LogInformation("[perf] Reputation lookups for {CreatureCount} people took {ElapsedMs}ms",
            nearbyPeopleRaw.Count, reputationStopwatch.ElapsedMilliseconds);

        return nearbyPeopleRaw
            .Select(x => new SceneCreatureInfo(x.Name, x.CreatureTypeName, x.GenderName, x.Profession, x.Level,
                query.CurrentDate.Year - x.BirthYear, factionNamesByCreature.GetValueOrDefault(x.Id, []), x.State,
                reputationByCreature.GetValueOrDefault(x.Id, 0)))
            .ToArray();
    }

    private async Task<IReadOnlyCollection<SceneExitInfo>> BuildExitInfos(IReadOnlyCollection<RoomConnector> connectors,
        CancellationToken cancellationToken) {
        var destinationIds = connectors
            .Where(c => c.DestinationRoomId != null)
            .Select(c => c.DestinationRoomId!.Value)
            .ToArray();
        var destinationRoomsById = await buildingService.GetRoomsByIds(destinationIds, cancellationToken);

        return connectors.Select(c => {
            var destinationName = c.DestinationRoomId != null && destinationRoomsById.TryGetValue(c.DestinationRoomId.Value, out var destinationRoom)
                ? destinationRoom.Name
                : "Outside";
            return new SceneExitInfo(c.Description, destinationName, c.IsLocked);
        }).ToArray();
    }

    private static string GetPropType(Prop prop) {
        return prop switch {
            Workstation w => w.WorkstationType.ToString(),
            Bed => "Bed",
            Seat => "Seat",
            Container => "Container",
            Trigger => "Trigger",
            _ => prop.GetType().Name
        };
    }
}
