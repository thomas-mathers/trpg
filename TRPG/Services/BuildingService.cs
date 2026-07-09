using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Services;

internal record ExitMatch(bool Matched, Guid? DestinationRoomId);

internal record RoomSummary(
    string RoomName,
    string RoomDescription,
    int RoomFloorNumber,
    Guid BuildingId,
    string BuildingName,
    BuildingType BuildingType,
    string? OwnerName,
    string? FactionName,
    string? FactionDescription
);

internal class BuildingService(TrpgDbContext context, IMemoryCache cache)
{
    public async Task AddOwner(
        Guid buildingId,
        Guid ownerId,
        CancellationToken cancellationToken = default
    )
    {
        var worldId = await context
            .Buildings.Where(b => b.Id == buildingId)
            .Select(b => b.WorldId)
            .FirstAsync(cancellationToken);

        context.BuildingOwners.Add(
            new BuildingOwner
            {
                Id = Guid.NewGuid(),
                BuildingId = buildingId,
                OwnerId = ownerId,
                WorldId = worldId,
            }
        );
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Building?> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        return await cache.GetOrCreateAsync(
            $"building:{id}",
            _ =>
                context
                    .Buildings.AsNoTracking()
                    .FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
        );
    }

    public async Task<RoomSummary?> GetRoomSummary(
        Guid roomId,
        CancellationToken cancellationToken = default
    )
    {
        return await cache.GetOrCreateAsync(
            $"roomSummary:{roomId}",
            async _ =>
                await (
                    from r in context.Rooms.AsNoTracking()
                    where r.Id == roomId
                    join b in context.Buildings.AsNoTracking() on r.BuildingId equals b.Id
                    join bo in context.BuildingOwners on b.Id equals bo.BuildingId into ownersGroup
                    from bo in ownersGroup.DefaultIfEmpty()
                    join owner in context.Creatures on bo.OwnerId equals owner.Id into ownerGroup
                    from owner in ownerGroup.DefaultIfEmpty()
                    join f in context.Factions on b.FactionId equals (Guid?)f.Id into factionGroup
                    from f in factionGroup.DefaultIfEmpty()
                    select new RoomSummary(
                        r.Name,
                        r.Description,
                        r.FloorNumber,
                        b.Id,
                        b.Name,
                        b.BuildingType,
                        owner != null ? owner.Name : null,
                        f != null ? f.Name : null,
                        f != null ? f.Description : null
                    )
                ).FirstOrDefaultAsync(cancellationToken)
        );
    }

    public async Task<Building?> GetByNameInState(
        Guid stateId,
        string name,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Buildings.AsNoTracking()
            .FirstOrDefaultAsync(
                b => b.StateId == stateId && EF.Functions.ILike(b.Name, name),
                cancellationToken
            );
    }

    public async Task<Room?> GetEntranceRoom(
        Guid buildingId,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Rooms.AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.BuildingId == buildingId && r.FloorNumber == 0,
                cancellationToken
            );
    }

    public async Task<ExitMatch> FindExitByDestinationName(
        Guid roomId,
        string destinationName,
        CancellationToken cancellationToken = default
    )
    {
        var connectors = await context
            .Props.AsNoTracking()
            .Where(p => p.RoomId == roomId)
            .OfType<RoomConnector>()
            .ToArrayAsync(cancellationToken);

        if (destinationName.Equals("Outside", StringComparison.OrdinalIgnoreCase))
        {
            return connectors.Any(c => c.DestinationRoomId == null)
                ? new ExitMatch(true, null)
                : new ExitMatch(false, null);
        }

        var destinationIds = connectors
            .Where(c => c.DestinationRoomId != null)
            .Select(c => c.DestinationRoomId!.Value)
            .ToHashSet();

        var destinationRoomId = await context
            .Rooms.Where(r =>
                destinationIds.Contains(r.Id) && EF.Functions.ILike(r.Name, destinationName)
            )
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return destinationRoomId != null
            ? new ExitMatch(true, destinationRoomId)
            : new ExitMatch(false, null);
    }

    public async Task<IReadOnlyDictionary<Guid, Room>> GetRoomsByIds(
        IReadOnlyCollection<Guid> roomIds,
        CancellationToken cancellationToken = default
    )
    {
        var result = new Dictionary<Guid, Room>();
        var missingIds = new List<Guid>();
        foreach (var id in roomIds)
        {
            if (cache.TryGetValue($"room:{id}", out Room? room) && room != null)
            {
                result[id] = room;
            }
            else
            {
                missingIds.Add(id);
            }
        }

        if (missingIds.Count > 0)
        {
            var fetched = await context
                .Rooms.AsNoTracking()
                .Where(r => missingIds.Contains(r.Id))
                .ToArrayAsync(cancellationToken);
            foreach (var room in fetched)
            {
                cache.Set($"room:{room.Id}", room);
                result[room.Id] = room;
            }
        }

        return result;
    }

    public async Task<IReadOnlyCollection<Building>> GetAllByStateId(
        Guid stateId,
        CancellationToken cancellationToken = default
    )
    {
        var list = await context
            .Buildings.AsNoTracking()
            .Where(b => b.StateId == stateId)
            .ToArrayAsync(cancellationToken);
        return list;
    }

    public async Task<IReadOnlyCollection<Prop>> GetStaticPropsByRoomId(
        Guid roomId,
        CancellationToken cancellationToken = default
    )
    {
        var props = await cache.GetOrCreateAsync<Prop[]>(
            $"staticProps:{roomId}",
            async _ =>
                (
                    await context
                        .Props.AsNoTracking()
                        .Where(p => p.RoomId == roomId)
                        .ToArrayAsync(cancellationToken)
                )
                    .Where(p => p is not RoomConnector)
                    .ToArray()
        );
        return props ?? [];
    }

    public async Task<IReadOnlyCollection<RoomConnector>> GetConnectorsByRoomId(
        Guid roomId,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Props.AsNoTracking()
            .Where(p => p.RoomId == roomId)
            .OfType<RoomConnector>()
            .ToArrayAsync(cancellationToken);
    }

    // Deliberately uncached, like GetConnectorsByRoomId — OccupantId is mutated at runtime by SetWorkstationOccupant.
    public async Task<IReadOnlyCollection<Workstation>> GetWorkstationsByRoomId(
        Guid roomId,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Props.AsNoTracking()
            .Where(p => p.RoomId == roomId)
            .OfType<Workstation>()
            .ToArrayAsync(cancellationToken);
    }

    public async Task SetWorkstationOccupant(
        Guid workstationId,
        Guid? occupantId,
        CancellationToken cancellationToken = default
    )
    {
        await context
            .Props.OfType<Workstation>()
            .Where(w => w.Id == workstationId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(w => w.OccupantId, occupantId),
                cancellationToken
            );
    }

    public async Task<IReadOnlyCollection<BuildingOwner>> GetAllOwnersByBuildingId(
        Guid buildingId,
        CancellationToken cancellationToken = default
    )
    {
        var list = await context
            .BuildingOwners.AsNoTracking()
            .Where(o => o.BuildingId == buildingId)
            .ToArrayAsync(cancellationToken);
        return list;
    }

    public async Task<IReadOnlyCollection<Building>> GetAllByLocation(
        Guid stateId,
        Guid? cityId,
        Guid? districtId,
        CancellationToken cancellationToken = default
    )
    {
        var buildings = await cache.GetOrCreateAsync<Building[]>(
            $"nearbyBuildings:{stateId}:{cityId}:{districtId}",
            async _ =>
                await context
                    .Buildings.AsNoTracking()
                    .Where(b =>
                        b.StateId == stateId && b.CityId == cityId && b.DistrictId == districtId
                    )
                    .ToArrayAsync(cancellationToken)
        );
        return buildings ?? [];
    }

    public async Task RemoveOwner(
        Guid buildingId,
        Guid ownerId,
        CancellationToken cancellationToken = default
    )
    {
        await context
            .BuildingOwners.Where(o => o.BuildingId == buildingId && o.OwnerId == ownerId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<RoomConnector?> GetFrontDoor(
        Guid roomId,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Props.AsNoTracking()
            .Where(p => p.RoomId == roomId)
            .OfType<RoomConnector>()
            .FirstOrDefaultAsync(c => c.DestinationRoomId == null, cancellationToken);
    }

    public async Task<bool?> SetFrontDoorLocked(
        Guid buildingId,
        bool isLocked,
        CancellationToken cancellationToken = default
    )
    {
        var updatedCount = await (
            from c in context.Props.OfType<RoomConnector>()
            join r in context.Rooms on c.RoomId equals r.Id
            where r.BuildingId == buildingId && r.FloorNumber == 0 && c.DestinationRoomId == null
            select c
        ).ExecuteUpdateAsync(s => s.SetProperty(c => c.IsLocked, isLocked), cancellationToken);

        return updatedCount > 0 ? isLocked : null;
    }

    public async Task<IReadOnlyList<Guid>> GetKeyItemIds(
        Guid roomConnectorId,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .RoomConnectorKeys.Where(k => k.RoomConnectorId == roomConnectorId)
            .Select(k => k.ItemId)
            .ToArrayAsync(cancellationToken);
    }
}
