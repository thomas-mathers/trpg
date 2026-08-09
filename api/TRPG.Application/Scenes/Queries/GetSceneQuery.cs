using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TRPG.Application.Buildings.Queries;
using TRPG.Application.Creatures;
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

public record SceneDateInfo(int Year, string MonthName, int Day, string WeekdayName, int Hour);

public record SceneStateInfo(string Name, string? Description);

public record SceneDistrictInfo(Guid Id, string Name, DistrictType Type);

public record SceneCityInfo(string Name, string? Description);

public record SceneBuildingInfo(
    string Name,
    BuildingType Type,
    string? OwnerName,
    string? FactionName,
    string? FactionDescription
);

public abstract record SceneExitDestination(string Name);

public sealed record SceneDistrictExitDestination(string Name, DistrictType DistrictType)
    : SceneExitDestination(Name);

public sealed record SceneBuildingExitDestination(string Name, BuildingType BuildingType)
    : SceneExitDestination(Name);

public sealed record SceneRoomExitDestination(string Name, BuildingType BuildingType)
    : SceneExitDestination(Name);

public sealed record SceneWildernessExitDestination(string Name) : SceneExitDestination(Name);

public record SceneExitInfo(string Description, SceneExitDestination Destination, bool IsLocked);

public record SceneRoomInfo(string Name, string Description, int FloorNumber);

public record ScenePropInfo(Guid Id, string Name, string Description, string Type);

public record SceneCreatureInfo(
    Guid Id,
    string Name,
    CreatureType CreatureType,
    Gender Gender,
    Profession? Profession,
    int Level,
    int Age,
    IReadOnlyCollection<string> FactionNames,
    CreatureState? State,
    int? Reputation,
    int Gold,
    int CurrentHp,
    int MaximumHp,
    int CurrentAp,
    int MaximumAp,
    int CurrentMp,
    int MaximumMp,
    int ExperienceCurrent,
    int ExperienceToNextLevel,
    int Strength,
    int Dexterity,
    int Intelligence,
    int Endurance,
    int Stamina,
    int Mana,
    int Defense,
    float MovementSpeed,
    float PhysicalResistance,
    float FireResistance,
    float IceResistance,
    float LightningResistance,
    float PoisonResistance,
    float MagicResistance
);

public record SceneNearbyBuildingInfo(Guid Id, string Name, BuildingType Type);

public record SceneResult(
    SceneDateInfo CurrentDate,
    SceneStateInfo? State,
    SceneCityInfo? City,
    SceneDistrictInfo? District,
    SceneBuildingInfo? Building,
    SceneRoomInfo? Room,
    SceneCreatureInfo Player,
    IReadOnlyCollection<SceneExitInfo> Exits,
    IReadOnlyCollection<ScenePropInfo> NearbyProps,
    IReadOnlyCollection<SceneCreatureInfo> NearbyCreatures,
    IReadOnlyCollection<SceneNearbyBuildingInfo> NearbyBuildings
);

internal record SceneLocationDetails(
    SceneBuildingInfo? Building,
    SceneRoomInfo? Room,
    string? RegionDescription,
    IReadOnlyCollection<ScenePropInfo> NearbyProps,
    IReadOnlyCollection<SceneNearbyBuildingInfo> NearbyBuildings
);

internal class GetSceneQueryHandler(
    TrpgDbContext context,
    GetStateByIdQueryHandler getStateById,
    GetCityByIdQueryHandler getCityById,
    GetCityByStateIdQueryHandler getCityByStateId,
    GetDistrictByIdQueryHandler getDistrictById,
    GetRoomSummaryQueryHandler getRoomSummary,
    GetStaticPropsByLocationIdQueryHandler getStaticPropsByLocationId,
    GetConnectorsByLocationIdQueryHandler getConnectorsByLocationId,
    GetAllBuildingsByLocationQueryHandler getAllBuildingsByLocation,
    GetNearbyCreaturesQueryHandler getNearbyCreatures,
    GetEffectiveReputationsQueryHandler getEffectiveReputations,
    GetTotalCharacterXpFromSkillsQueryHandler getTotalCharacterXpFromSkills,
    ILogger<GetSceneQueryHandler> logger
)
{
    public async Task<SceneResult> Handle(
        GetSceneQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();

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

        logger.LogInformation(
            "[perf] GetScene ({Branch}) took {ElapsedMs}ms, {CreatureCount} nearby people",
            player.RoomId != null ? "indoor" : "outdoor",
            stopwatch.ElapsedMilliseconds,
            nearbyPeople.Count
        );

        return new SceneResult(
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
        CreatureSummary player,
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
        CreatureSummary creature,
        int currentYear,
        IReadOnlyCollection<string> factionNames,
        CreatureState? state,
        int? reputation,
        int totalCharacterXp
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
            creature.MagicResistance
        );
    }

    private async Task<SceneCityInfo?> BuildCityInfo(
        CreatureSummary player,
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
        CreatureSummary player,
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

    private async Task<SceneLocationDetails> BuildIndoorScene(
        CreatureSummary player,
        CancellationToken cancellationToken
    )
    {
        var roomSummary = await getRoomSummary.Handle(
            new GetRoomSummaryQuery { RoomId = player.RoomId!.Value },
            cancellationToken
        );

        var props = await getStaticPropsByLocationId.Handle(
            new GetStaticPropsByLocationIdQuery { LocationId = player.LocationId },
            cancellationToken
        );

        var buildingInfo = new SceneBuildingInfo(
            roomSummary!.BuildingName,
            roomSummary.BuildingType,
            roomSummary.OwnerName,
            roomSummary.FactionName,
            roomSummary.FactionDescription
        );
        var roomInfo = new SceneRoomInfo(
            roomSummary.RoomName,
            roomSummary.RoomDescription,
            roomSummary.RoomFloorNumber
        );
        var nearbyProps = props
            .Select(p => new ScenePropInfo(p.Id, p.Name, p.Description, GetPropType(p)))
            .ToArray();

        return new SceneLocationDetails(buildingInfo, roomInfo, null, nearbyProps, []);
    }

    private async Task<SceneLocationDetails> BuildOutdoorScene(
        CreatureSummary player,
        State? state,
        CancellationToken cancellationToken
    )
    {
        var buildings = await getAllBuildingsByLocation.Handle(
            new GetAllBuildingsByLocationQuery
            {
                StateId = player.StateId,
                CityId = player.CityId,
                DistrictId = player.DistrictId,
            },
            cancellationToken
        );

        var nearbyBuildings = buildings
            .Select(b => new SceneNearbyBuildingInfo(b.Id, b.Name, b.BuildingType))
            .ToArray();

        return new SceneLocationDetails(null, null, state?.Description, [], nearbyBuildings);
    }

    private async Task<IReadOnlyCollection<SceneCreatureInfo>> BuildNearbyPeopleInfos(
        GetSceneQuery query,
        IReadOnlyCollection<CreatureSummary> nearby,
        CancellationToken cancellationToken
    )
    {
        if (nearby.Count == 0)
        {
            return [];
        }

        var nearbyCreatureIds = nearby.Select(x => x.Id).ToArray();
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

        var reputationByCreature = await getEffectiveReputations.Handle(
            new GetEffectiveReputationsQuery
            {
                ObserverCreatureId = query.PlayerId,
                TargetCreatureIds = nearbyCreatureIds,
                FactionIdsByCreature = factionIdsByCreature,
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
                    totalCharacterXp: 0
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
        var typedConnectors = connectors
            .Where(connector => connector.DestinationType != LocationDestinationType.Wilderness)
            .ToArray();
        var destinationLocationIds = typedConnectors
            .Select(connector => connector.DestinationLocationId)
            .ToArray();
        var destinations = await context
            .Locations.AsNoTracking()
            .Where(location => destinationLocationIds.Contains(location.Id))
            .ToDictionaryAsync(location => location.Id, cancellationToken);
        var districtIds = typedConnectors
            .Where(connector => connector.DestinationType == LocationDestinationType.District)
            .Select(connector => destinations[connector.DestinationLocationId].DistrictId)
            .OfType<Guid>()
            .ToArray();
        var districts = await context
            .Districts.AsNoTracking()
            .Where(district => districtIds.Contains(district.Id))
            .ToDictionaryAsync(district => district.Id, cancellationToken);
        var roomIds = typedConnectors
            .Where(connector => connector.DestinationType == LocationDestinationType.Room)
            .Select(connector => destinations[connector.DestinationLocationId].RoomId)
            .OfType<Guid>()
            .ToArray();
        var rooms = await context
            .Rooms.AsNoTracking()
            .Where(room => roomIds.Contains(room.Id))
            .ToDictionaryAsync(room => room.Id, cancellationToken);
        var buildingIds = rooms.Values.Select(room => room.BuildingId).ToArray();
        var buildings = await context
            .Buildings.AsNoTracking()
            .Where(building => buildingIds.Contains(building.Id))
            .ToDictionaryAsync(building => building.Id, cancellationToken);

        return connectors
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
                connector.IsLocked
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
        if (connector.DestinationType == LocationDestinationType.District)
        {
            var districtId = location!.DistrictId!.Value;
            var district = districts[districtId];
            return new SceneDistrictExitDestination(
                connector.DestinationLabel,
                district.DistrictType
            );
        }

        if (connector.DestinationType == LocationDestinationType.Room)
        {
            var roomId = location!.RoomId!.Value;
            var building = buildings[rooms[roomId].BuildingId];
            return sourceIsRoom
                ? new SceneRoomExitDestination(connector.DestinationLabel, building.BuildingType)
                : new SceneBuildingExitDestination(
                    connector.DestinationLabel,
                    building.BuildingType
                );
        }

        return new SceneWildernessExitDestination(connector.DestinationLabel);
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
