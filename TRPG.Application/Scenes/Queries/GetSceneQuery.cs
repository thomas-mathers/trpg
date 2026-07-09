using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TRPG.Application.Buildings.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Reputations.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Scenes.Queries;

internal class GetSceneQuery
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required InGameDate CurrentDate { get; init; }
}

public record SceneStateInfo(string Name, string? Description);

public record SceneDistrictInfo(string Name, string Type, bool IsCurrent);

public record SceneCityInfo(
    string Name,
    string? Description,
    IReadOnlyCollection<SceneDistrictInfo> Districts
);

public record SceneBuildingInfo(
    string Name,
    string Type,
    string? OwnerName,
    string? FactionName,
    string? FactionDescription
);

public record SceneExitInfo(string Description, string DestinationRoomName, bool IsLocked);

public record SceneRoomInfo(
    string Name,
    string Description,
    int FloorNumber,
    IReadOnlyCollection<SceneExitInfo> Exits
);

public record ScenePlayerInfo(
    string Name,
    string CreatureType,
    string Gender,
    string Profession,
    int Level,
    int Gold,
    int Age
);

public record ScenePropInfo(string Name, string Description, string Type);

public record SceneCreatureInfo(
    string Name,
    string CreatureType,
    string Gender,
    string Profession,
    int Level,
    int Age,
    IReadOnlyCollection<string> FactionNames,
    string State,
    int Reputation
);

public record SceneNearbyBuildingInfo(string Name, string Type);

public record SceneResult(
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
    string GenderName
);

internal record SceneLocationDetails(
    SceneBuildingInfo? Building,
    SceneRoomInfo? Room,
    string? RegionDescription,
    IReadOnlyCollection<ScenePropInfo> NearbyProps,
    IReadOnlyCollection<SceneCreatureInfo> NearbyPeople,
    IReadOnlyCollection<SceneNearbyBuildingInfo> NearbyBuildings
);

internal class GetSceneQueryHandler(
    TrpgDbContext context,
    GetStateByIdQueryHandler getStateById,
    GetCityByIdQueryHandler getCityById,
    GetAllDistrictsByCityIdQueryHandler getAllDistrictsByCityId,
    GetRoomSummaryQueryHandler getRoomSummary,
    GetStaticPropsByRoomIdQueryHandler getStaticPropsByRoomId,
    GetConnectorsByRoomIdQueryHandler getConnectorsByRoomId,
    GetAllBuildingsByLocationQueryHandler getAllBuildingsByLocation,
    GetRoomsByIdsQueryHandler getRoomsByIds,
    GetAllNearbyCreaturesQueryHandler getAllNearbyCreatures,
    GetEffectiveReputationsQueryHandler getEffectiveReputations,
    ILogger<GetSceneQueryHandler> logger
)
{
    public async Task<SceneResult> Handle(
        GetSceneQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();

        var bootstrap = await GetBootstrap(query.PlayerId, cancellationToken);
        var state = await getStateById.Handle(
            new GetStateByIdQuery { Id = bootstrap.StateId },
            cancellationToken
        );
        var cityInfo = await BuildCityInfo(bootstrap, cancellationToken);

        var details =
            bootstrap.RoomId != null
                ? await BuildIndoorScene(query, bootstrap, cancellationToken)
                : await BuildOutdoorScene(query, bootstrap, state, cancellationToken);

        logger.LogInformation(
            "[perf] GetScene ({Branch}) took {ElapsedMs}ms, {CreatureCount} nearby people",
            bootstrap.RoomId != null ? "indoor" : "outdoor",
            stopwatch.ElapsedMilliseconds,
            details.NearbyPeople.Count
        );

        return new SceneResult(
            query.CurrentDate,
            new SceneStateInfo(state!.Name, details.RegionDescription),
            cityInfo,
            details.Building,
            details.Room,
            new ScenePlayerInfo(
                bootstrap.PlayerName,
                bootstrap.CreatureTypeName,
                bootstrap.GenderName,
                bootstrap.Profession.ToString()!,
                bootstrap.Level,
                bootstrap.Gold,
                query.CurrentDate.Year - bootstrap.BirthYear
            ),
            details.NearbyProps,
            details.NearbyPeople,
            details.NearbyBuildings
        );
    }

    private async Task<SceneBootstrap> GetBootstrap(
        Guid playerId,
        CancellationToken cancellationToken
    )
    {
        return await context
            .Creatures.AsNoTracking()
            .Where(p => p.Id == playerId)
            .Select(p => new SceneBootstrap(
                p.Name,
                p.Profession,
                p.Level,
                p.Gold,
                p.BirthYear,
                p.StateId,
                p.CityId,
                p.DistrictId,
                p.RoomId,
                p.CreatureType.ToString(),
                p.Gender.ToString()
            ))
            .FirstAsync(cancellationToken);
    }

    private async Task<SceneCityInfo?> BuildCityInfo(
        SceneBootstrap bootstrap,
        CancellationToken cancellationToken
    )
    {
        if (bootstrap.CityId == null)
        {
            return null;
        }

        var city = await getCityById.Handle(
            new GetCityByIdQuery { Id = bootstrap.CityId.Value },
            cancellationToken
        );
        var districts = await getAllDistrictsByCityId.Handle(
            new GetAllDistrictsByCityIdQuery { CityId = bootstrap.CityId.Value },
            cancellationToken
        );
        var districtInfos = districts
            .Select(d => new SceneDistrictInfo(
                d.Name,
                d.DistrictType.ToString(),
                d.Id == bootstrap.DistrictId
            ))
            .ToArray();
        return new SceneCityInfo(city!.Name, city.Description, districtInfos);
    }

    private async Task<SceneLocationDetails> BuildIndoorScene(
        GetSceneQuery query,
        SceneBootstrap bootstrap,
        CancellationToken cancellationToken
    )
    {
        var roomSummary = await getRoomSummary.Handle(
            new GetRoomSummaryQuery { RoomId = bootstrap.RoomId!.Value },
            cancellationToken
        );

        var props = await getStaticPropsByRoomId.Handle(
            new GetStaticPropsByRoomIdQuery { RoomId = bootstrap.RoomId.Value },
            cancellationToken
        );
        var connectors = await getConnectorsByRoomId.Handle(
            new GetConnectorsByRoomIdQuery { RoomId = bootstrap.RoomId.Value },
            cancellationToken
        );
        var exitInfos = await BuildExitInfos(connectors, cancellationToken);

        var nearbyPeople = await BuildNearbyPeople(query, bootstrap, cancellationToken);

        var buildingInfo = new SceneBuildingInfo(
            roomSummary!.BuildingName,
            roomSummary.BuildingType.ToString(),
            roomSummary.OwnerName,
            roomSummary.FactionName,
            roomSummary.FactionDescription
        );
        var roomInfo = new SceneRoomInfo(
            roomSummary.RoomName,
            roomSummary.RoomDescription,
            roomSummary.RoomFloorNumber,
            exitInfos
        );
        var nearbyProps = props
            .Select(p => new ScenePropInfo(p.Name, p.Description, GetPropType(p)))
            .ToArray();

        return new SceneLocationDetails(
            buildingInfo,
            roomInfo,
            null,
            nearbyProps,
            nearbyPeople,
            []
        );
    }

    private async Task<SceneLocationDetails> BuildOutdoorScene(
        GetSceneQuery query,
        SceneBootstrap bootstrap,
        State? state,
        CancellationToken cancellationToken
    )
    {
        var buildings = await getAllBuildingsByLocation.Handle(
            new GetAllBuildingsByLocationQuery
            {
                StateId = bootstrap.StateId,
                CityId = bootstrap.CityId,
                DistrictId = bootstrap.DistrictId,
            },
            cancellationToken
        );
        var nearbyBuildings = buildings
            .Select(b => new SceneNearbyBuildingInfo(b.Name, b.BuildingType.ToString()))
            .ToArray();

        var nearbyPeople = await BuildNearbyPeople(query, bootstrap, cancellationToken);

        return new SceneLocationDetails(
            null,
            null,
            state?.Description,
            [],
            nearbyPeople,
            nearbyBuildings
        );
    }

    private async Task<IReadOnlyCollection<SceneCreatureInfo>> BuildNearbyPeople(
        GetSceneQuery query,
        SceneBootstrap bootstrap,
        CancellationToken cancellationToken
    )
    {
        var creatureLocation = new CreatureLocation(
            query.WorldId,
            bootstrap.RoomId,
            bootstrap.StateId,
            bootstrap.DistrictId
        );
        var nearbyPeopleRaw = await getAllNearbyCreatures.Handle(
            new GetAllNearbyCreaturesQuery
            {
                Location = creatureLocation,
                ExcludingCreatureId = query.PlayerId,
            },
            cancellationToken
        );

        var nearbyCreatureIds = nearbyPeopleRaw.Select(x => x.Id).ToArray();
        var factionMembershipsByCreature = await (
            from fm in context.FactionMembers
            where nearbyCreatureIds.Contains(fm.CreatureId)
            join f in context.Factions on fm.FactionId equals f.Id
            select new
            {
                fm.CreatureId,
                fm.FactionId,
                f.Name,
                f.IsCityFaction,
            }
        )
            .GroupBy(x => x.CreatureId)
            .ToDictionaryAsync(
                g => g.Key,
                g => new
                {
                    FactionIds = g.Select(x => x.FactionId).ToArray(),
                    FactionNames = g.Where(x => !x.IsCityFaction).Select(x => x.Name).ToArray(),
                },
                cancellationToken
            );

        var factionNamesByCreature = factionMembershipsByCreature.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.FactionNames
        );
        var factionIdsByCreature = factionMembershipsByCreature.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.FactionIds
        );

        var reputationStopwatch = Stopwatch.StartNew();
        var reputationByCreature = await getEffectiveReputations.Handle(
            new GetEffectiveReputationsQuery
            {
                ObserverCreatureId = query.PlayerId,
                TargetCreatureIds = nearbyCreatureIds,
                FactionIdsByCreature = factionIdsByCreature,
            },
            cancellationToken
        );
        logger.LogInformation(
            "[perf] Reputation lookups for {CreatureCount} people took {ElapsedMs}ms",
            nearbyPeopleRaw.Count,
            reputationStopwatch.ElapsedMilliseconds
        );

        return nearbyPeopleRaw
            .Select(x => new SceneCreatureInfo(
                x.Name,
                x.CreatureTypeName,
                x.GenderName,
                x.Profession,
                x.Level,
                query.CurrentDate.Year - x.BirthYear,
                factionNamesByCreature.GetValueOrDefault(x.Id, []),
                x.State,
                reputationByCreature.GetValueOrDefault(x.Id, 0)
            ))
            .ToArray();
    }

    private async Task<IReadOnlyCollection<SceneExitInfo>> BuildExitInfos(
        IReadOnlyCollection<RoomConnector> connectors,
        CancellationToken cancellationToken
    )
    {
        var destinationIds = connectors
            .Where(c => c.DestinationRoomId != null)
            .Select(c => c.DestinationRoomId!.Value)
            .ToArray();
        var destinationRoomsById = await getRoomsByIds.Handle(
            new GetRoomsByIdsQuery { RoomIds = destinationIds },
            cancellationToken
        );

        return connectors
            .Select(c =>
            {
                var destinationName =
                    c.DestinationRoomId != null
                    && destinationRoomsById.TryGetValue(
                        c.DestinationRoomId.Value,
                        out var destinationRoom
                    )
                        ? destinationRoom.Name
                        : "Outside";
                return new SceneExitInfo(c.Description, destinationName, c.IsLocked);
            })
            .ToArray();
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
