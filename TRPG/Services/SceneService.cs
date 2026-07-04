using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

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

internal record ScenePlayerInfo(string Name, string Race, string Profession, int Level, int Gold, int Age);

internal record ScenePropInfo(string Name, string Description, string Type);

internal record ScenePersonInfo(
    string Name,
    string Race,
    string Profession,
    int Level,
    int Age,
    IReadOnlyCollection<string> FactionNames,
    string State,
    int Reputation);

internal record SceneQuery(Guid WorldId, Guid PlayerId, InGameDate CurrentDate);

internal record SceneNearbyBuildingInfo(string Name, string Type);

internal record SceneResult(
    InGameDate CurrentDate,
    SceneStateInfo? State,
    SceneCityInfo? City,
    SceneBuildingInfo? Building,
    SceneRoomInfo? Room,
    ScenePlayerInfo Player,
    IReadOnlyCollection<ScenePropInfo> NearbyProps,
    IReadOnlyCollection<ScenePersonInfo> NearbyPeople,
    IReadOnlyCollection<SceneNearbyBuildingInfo> NearbyBuildings
);

internal class SceneService(
    TrpgDbContext context,
    JobCatchUpService jobCatchUpService,
    LockService lockService,
    ReputationService reputationService
) {
    public async Task<SceneResult> GetScene(SceneQuery query, CancellationToken cancellationToken = default) {
        var worldId = query.WorldId;
        var playerId = query.PlayerId;

        var bootstrap = await (
            from p in context.Persons
            where p.Id == playerId
            join race in context.Races on p.RaceId equals race.Id
            join region in context.States on p.StateId equals region.Id
            join city in context.Cities on p.CityId equals city.Id into cityGroup
            from city in cityGroup.DefaultIfEmpty()
            select new {
                RegionName = region.Name,
                RegionDescription = region.Description,
                CityName = city != null ? city.Name : null,
                CityDescription = city != null ? city.Description : null,
                PlayerName = p.Name,
                p.Profession,
                p.Level,
                p.Gold,
                p.BirthYear,
                p.StateId,
                p.CityId,
                p.DistrictId,
                p.RoomId,
                RaceName = race.Name
            }
        ).FirstAsync(cancellationToken);

        SceneBuildingInfo? buildingInfo = null;
        SceneRoomInfo? roomInfo = null;
        IReadOnlyCollection<ScenePropInfo> nearbyProps = [];
        IReadOnlyCollection<ScenePersonInfo> nearbyPeople = [];
        IReadOnlyCollection<SceneNearbyBuildingInfo> nearbyBuildings = [];
        string? regionDescription = null;

        SceneCityInfo? cityInfo = null;
        if (bootstrap.CityId != null) {
            var districts = await context.Districts
                .Where(d => d.CityId == bootstrap.CityId.Value)
                .ToArrayAsync(cancellationToken);
            var districtInfos = districts
                .Select(d => new SceneDistrictInfo(d.Name, d.DistrictType.ToString(), d.Id == bootstrap.DistrictId))
                .ToArray();
            cityInfo = new SceneCityInfo(bootstrap.CityName!, bootstrap.CityDescription, districtInfos);
        }

        if (bootstrap.RoomId != null) {
            await jobCatchUpService.CatchUpRoom(bootstrap.RoomId.Value, query.CurrentDate.Hour, cancellationToken);

            var roomAndBuilding = await (
                from r in context.Rooms
                where r.Id == bootstrap.RoomId.Value
                join b in context.Buildings on r.BuildingId equals b.Id
                select new {
                    RoomName = r.Name,
                    RoomDescription = r.Description,
                    r.FloorNumber,
                    BuildingId = b.Id,
                    BuildingName = b.Name,
                    BuildingDescription = b.Description,
                    b.BuildingType,
                    b.FactionId
                }
            ).FirstAsync(cancellationToken);

            var ownerName = await (
                from bo in context.BuildingOwners
                where bo.BuildingId == roomAndBuilding.BuildingId
                join owner in context.Persons on bo.OwnerId equals owner.Id
                select owner.Name
            ).FirstOrDefaultAsync(cancellationToken);

            string? factionName = null;
            string? factionDescription = null;
            if (roomAndBuilding.FactionId != null) {
                var faction = await context.Factions
                    .Where(f => f.Id == roomAndBuilding.FactionId.Value)
                    .Select(f => new { f.Name, f.Description })
                    .FirstOrDefaultAsync(cancellationToken);
                factionName = faction?.Name;
                factionDescription = faction?.Description;
            }

            var props = await context.Props
                .Where(p => p.RoomId == bootstrap.RoomId.Value)
                .ToArrayAsync(cancellationToken);

            var connectors = props.OfType<RoomConnector>().ToArray();
            var destinationIds = connectors
                .Where(c => c.DestinationRoomId != null)
                .Select(c => c.DestinationRoomId!.Value)
                .ToHashSet();

            var destinationNameById = destinationIds.Count > 0
                ? await context.Rooms
                    .Where(r => destinationIds.Contains(r.Id))
                    .Select(r => new { r.Id, r.Name })
                    .ToDictionaryAsync(r => r.Id, r => r.Name, cancellationToken)
                : [];

            var nearbyPeopleRaw = await (
                from p in context.Persons
                where p.WorldId == worldId && p.RoomId == bootstrap.RoomId.Value && p.Id != playerId
                join race in context.Races on p.RaceId equals race.Id
                select new {
                    p.Id, p.Name, RaceName = race.Name, Profession = p.Profession.ToString(), p.Level, p.BirthYear,
                    State = p.State.ToString()
                }
            ).ToArrayAsync(cancellationToken);

            var nearbyPersonIds = nearbyPeopleRaw.Select(x => x.Id).ToArray();
            var factionNamesByPerson = (await (
                    from fm in context.FactionMembers
                    where nearbyPersonIds.Contains(fm.PersonId)
                    join f in context.Factions on fm.FactionId equals f.Id
                    select new { fm.PersonId, f.Name }
                ).ToArrayAsync(cancellationToken))
                .GroupBy(x => x.PersonId)
                .ToDictionary(g => g.Key, g => (IReadOnlyCollection<string>) g.Select(x => x.Name).ToArray());

            var nearbyPeopleList = new List<ScenePersonInfo>();
            foreach (var x in nearbyPeopleRaw) {
                var reputation = await reputationService.GetEffectiveReputation(playerId, x.Id, cancellationToken);
                nearbyPeopleList.Add(new ScenePersonInfo(x.Name, x.RaceName, x.Profession, x.Level,
                    query.CurrentDate.Year - x.BirthYear, factionNamesByPerson.GetValueOrDefault(x.Id, []), x.State,
                    reputation));
            }

            nearbyPeople = nearbyPeopleList;

            buildingInfo = new SceneBuildingInfo(
                roomAndBuilding.BuildingName,
                roomAndBuilding.BuildingType.ToString(),
                ownerName,
                factionName,
                factionDescription
            );

            var exitInfos = new List<SceneExitInfo>();
            foreach (var c in connectors) {
                var destinationName = c.DestinationRoomId != null &&
                                      destinationNameById.TryGetValue(c.DestinationRoomId.Value, out var name)
                    ? name
                    : "Outside";
                if (c.DestinationRoomId == null) {
                    await lockService.SyncScheduleLock(roomAndBuilding.BuildingId, roomAndBuilding.BuildingType,
                        query.CurrentDate.Hour, cancellationToken);
                }

                exitInfos.Add(new SceneExitInfo(c.Description, destinationName, c.IsLocked));
            }

            roomInfo = new SceneRoomInfo(
                roomAndBuilding.RoomName,
                roomAndBuilding.RoomDescription,
                roomAndBuilding.FloorNumber,
                exitInfos
            );

            nearbyProps = props.Where(p => p is not RoomConnector)
                .Select(p => new ScenePropInfo(p.Name, p.Description, GetPropType(p)))
                .ToArray();
        }
        else {
            regionDescription = bootstrap.RegionDescription;

            if (bootstrap.DistrictId != null) {
                await jobCatchUpService.CatchUpDistrict(worldId, bootstrap.DistrictId.Value, query.CurrentDate.Hour,
                    cancellationToken);
            }

            nearbyBuildings = await context.Buildings
                .Where(b => b.StateId == bootstrap.StateId && b.CityId == bootstrap.CityId &&
                            b.DistrictId == bootstrap.DistrictId)
                .Select(b => new SceneNearbyBuildingInfo(b.Name, b.BuildingType.ToString()))
                .ToArrayAsync(cancellationToken);

            var nearbyPeopleRaw = await (
                from p in context.Persons
                where p.WorldId == worldId && p.StateId == bootstrap.StateId && p.DistrictId == bootstrap.DistrictId &&
                      p.RoomId == null && p.Id != playerId
                join race in context.Races on p.RaceId equals race.Id
                select new {
                    p.Id, p.Name, RaceName = race.Name, Profession = p.Profession.ToString(), p.Level, p.BirthYear,
                    State = p.State.ToString()
                }
            ).ToArrayAsync(cancellationToken);

            var nearbyPersonIds = nearbyPeopleRaw.Select(x => x.Id).ToArray();
            var factionNamesByPerson = (await (
                    from fm in context.FactionMembers
                    where nearbyPersonIds.Contains(fm.PersonId)
                    join f in context.Factions on fm.FactionId equals f.Id
                    select new { fm.PersonId, f.Name }
                ).ToArrayAsync(cancellationToken))
                .GroupBy(x => x.PersonId)
                .ToDictionary(g => g.Key, g => (IReadOnlyCollection<string>) g.Select(x => x.Name).ToArray());

            var nearbyPeopleList = new List<ScenePersonInfo>();
            foreach (var x in nearbyPeopleRaw) {
                var reputation = await reputationService.GetEffectiveReputation(playerId, x.Id, cancellationToken);
                nearbyPeopleList.Add(new ScenePersonInfo(x.Name, x.RaceName, x.Profession, x.Level,
                    query.CurrentDate.Year - x.BirthYear, factionNamesByPerson.GetValueOrDefault(x.Id, []), x.State,
                    reputation));
            }

            nearbyPeople = nearbyPeopleList;
        }

        return new SceneResult(
            query.CurrentDate,
            new SceneStateInfo(bootstrap.RegionName, regionDescription),
            cityInfo,
            buildingInfo,
            roomInfo,
            new ScenePlayerInfo(bootstrap.PlayerName, bootstrap.RaceName, bootstrap.Profession.ToString(),
                bootstrap.Level, bootstrap.Gold, query.CurrentDate.Year - bootstrap.BirthYear),
            nearbyProps,
            nearbyPeople,
            nearbyBuildings
        );
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