using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

internal record SceneRegionInfo(string Name, string? Description, string Type, bool IsCapital);

internal record SceneBuildingInfo(string Name, string Type, string? OwnerName, string? FactionName, string? FactionDescription);

internal record SceneExitInfo(string Description, string DestinationRoomName, bool IsLocked);

internal record SceneRoomInfo(string Name, string Description, int FloorNumber, IReadOnlyCollection<SceneExitInfo> Exits);

internal record ScenePlayerInfo(string Name, string Race, string Profession, int Level, int Gold);

internal record ScenePropInfo(string Name, string Description, string Type);

internal record ScenePersonInfo(string Name, string Race, string Profession, int Level, string? FactionName);

internal record SceneNearbyBuildingInfo(string Name, string Type);

internal record SceneResult(
    SceneRegionInfo Region,
    SceneBuildingInfo? Building,
    SceneRoomInfo? Room,
    ScenePlayerInfo Player,
    IReadOnlyCollection<ScenePropInfo> NearbyProps,
    IReadOnlyCollection<ScenePersonInfo> NearbyPeople,
    IReadOnlyCollection<SceneNearbyBuildingInfo>? NearbyBuildings
);

internal class SceneService(TrpgDbContext context) {
    public async Task<SceneResult> GetScene(Guid worldId, Guid playerId, CancellationToken cancellationToken = default) {
        var bootstrap = await (
            from p in context.Persons where p.Id == playerId
            join race in context.Races on p.RaceId equals race.Id
            join region in context.Regions on p.RegionId equals region.Id
            select new {
                RegionName = region.Name,
                RegionDescription = region.Description,
                region.RegionType,
                region.IsCapital,
                PlayerName = p.Name,
                p.Profession,
                p.Level,
                p.Gold,
                p.RegionId,
                p.RoomId,
                RaceName = race.Name,
            }
        ).FirstAsync(cancellationToken);

        SceneBuildingInfo? buildingInfo = null;
        SceneRoomInfo? roomInfo = null;
        IReadOnlyCollection<ScenePropInfo> nearbyProps = [];
        IReadOnlyCollection<ScenePersonInfo> nearbyPeople = [];
        IReadOnlyCollection<SceneNearbyBuildingInfo>? nearbyBuildings = null;
        string? regionDescription = null;

        if (bootstrap.RoomId != null) {
            var roomAndBuilding = await (
                from r in context.Rooms where r.Id == bootstrap.RoomId.Value
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
                from bo in context.BuildingOwners where bo.BuildingId == roomAndBuilding.BuildingId
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

            nearbyPeople = await (
                from p in context.Persons
                where p.WorldId == worldId && p.RoomId == bootstrap.RoomId.Value && p.Id != playerId
                join race in context.Races on p.RaceId equals race.Id
                join fm in context.FactionMembers on p.Id equals fm.PersonId into fmGroup
                from fm in fmGroup.DefaultIfEmpty()
                join f in context.Factions on fm.FactionId equals f.Id into fGroup
                from f in fGroup.DefaultIfEmpty()
                select new ScenePersonInfo(p.Name, race.Name, p.Profession.ToString(), p.Level, f != null ? f.Name : null)
            ).ToArrayAsync(cancellationToken);

            buildingInfo = new SceneBuildingInfo(
                roomAndBuilding.BuildingName,
                roomAndBuilding.BuildingType.ToString(),
                ownerName,
                factionName,
                factionDescription
            );

            roomInfo = new SceneRoomInfo(
                roomAndBuilding.RoomName,
                roomAndBuilding.RoomDescription,
                roomAndBuilding.FloorNumber,
                connectors.Select(c => new SceneExitInfo(
                    c.Description,
                    c.DestinationRoomId != null && destinationNameById.TryGetValue(c.DestinationRoomId.Value, out var name)
                        ? name
                        : "Outside",
                    c.KeyItemId != null
                )).ToArray()
            );

            nearbyProps = props.Where(p => p is not RoomConnector)
                .Select(p => new ScenePropInfo(p.Name, p.Description, GetPropType(p)))
                .ToArray();
        } else {
            regionDescription = bootstrap.RegionDescription;

            nearbyBuildings = await context.Buildings
                .Where(b => b.RegionId == bootstrap.RegionId && b.BuildingType != BuildingType.House)
                .Select(b => new SceneNearbyBuildingInfo(b.Name, b.BuildingType.ToString()))
                .ToArrayAsync(cancellationToken);

            nearbyPeople = await (
                from p in context.Persons
                where p.WorldId == worldId && p.RegionId == bootstrap.RegionId && p.RoomId == null && p.Id != playerId
                join race in context.Races on p.RaceId equals race.Id
                join fm in context.FactionMembers on p.Id equals fm.PersonId into fmGroup
                from fm in fmGroup.DefaultIfEmpty()
                join f in context.Factions on fm.FactionId equals f.Id into fGroup
                from f in fGroup.DefaultIfEmpty()
                select new ScenePersonInfo(p.Name, race.Name, p.Profession.ToString(), p.Level, f != null ? f.Name : null)
            ).ToArrayAsync(cancellationToken);
        }

        return new SceneResult(
            new SceneRegionInfo(bootstrap.RegionName, regionDescription, bootstrap.RegionType.ToString(), bootstrap.IsCapital),
            buildingInfo,
            roomInfo,
            new ScenePlayerInfo(bootstrap.PlayerName, bootstrap.RaceName, bootstrap.Profession.ToString(), bootstrap.Level, bootstrap.Gold),
            nearbyProps,
            nearbyPeople,
            nearbyBuildings
        );
    }

    private static string GetPropType(Prop prop) => prop switch {
        Workstation w => w.WorkstationType.ToString(),
        Bed => "Bed",
        Seat => "Seat",
        Container => "Container",
        Trigger => "Trigger",
        _ => prop.GetType().Name
    };
}
