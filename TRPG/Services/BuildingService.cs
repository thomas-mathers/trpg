using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

internal record ExitMatch(bool Matched, Guid? DestinationRoomId);

internal class BuildingService(TrpgDbContext context) {
    public async Task AddOwner(Guid buildingId, Guid ownerId, CancellationToken cancellationToken = default) {
        context.BuildingOwners.Add(new BuildingOwner {
            Id = Guid.NewGuid(),
            BuildingId = buildingId,
            OwnerId = ownerId
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Building?> GetById(Guid id, CancellationToken cancellationToken = default) {
        return await context.Buildings.FindAsync([id], cancellationToken);
    }

    public async Task<Building?> GetByNameInState(Guid stateId, string name, CancellationToken cancellationToken = default) {
        return await context.Buildings.FirstOrDefaultAsync(b => b.StateId == stateId && b.Name == name, cancellationToken);
    }

    public async Task<Room?> GetEntranceRoom(Guid buildingId, CancellationToken cancellationToken = default) {
        return await context.Rooms.FirstOrDefaultAsync(r => r.BuildingId == buildingId && r.FloorNumber == 0, cancellationToken);
    }

    public async Task<ExitMatch> FindExitByDestinationName(Guid roomId, string destinationName, CancellationToken cancellationToken = default) {
        var connectors = await context.Props
            .Where(p => p.RoomId == roomId)
            .OfType<RoomConnector>()
            .ToArrayAsync(cancellationToken);

        if (destinationName == "Outside") {
            return connectors.Any(c => c.DestinationRoomId == null) ? new ExitMatch(true, null) : new ExitMatch(false, null);
        }

        var destinationIds = connectors
            .Where(c => c.DestinationRoomId != null)
            .Select(c => c.DestinationRoomId!.Value)
            .ToHashSet();

        var destinationRoomId = await context.Rooms
            .Where(r => destinationIds.Contains(r.Id) && r.Name == destinationName)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return destinationRoomId != null ? new ExitMatch(true, destinationRoomId) : new ExitMatch(false, null);
    }

    public async Task<IReadOnlyCollection<Room>> GetRoomByIds(HashSet<Guid> roomIds, CancellationToken cancellationToken = default) {
        return await context.Rooms.Where(b => roomIds.Contains(b.Id)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Building>> GetAllByStateId(Guid stateId,
        CancellationToken cancellationToken = default) {
        var list = await context.Buildings
            .Where(b => b.StateId == stateId)
            .ToArrayAsync(cancellationToken);
        return list;
    }

    public async Task<IReadOnlyCollection<Room>> GetEntranceRoomsByBuildingIds(IReadOnlyCollection<Guid> buildingIds,
        CancellationToken cancellationToken = default) {
        return await context.Rooms
            .Where(r => buildingIds.Contains(r.BuildingId) && r.FloorNumber == 0)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Prop>> GetAllPropsByRoomId(Guid roomId,
        CancellationToken cancellationToken = default) {
        var list = await context.Props
            .Where(p => p.RoomId == roomId)
            .ToArrayAsync(cancellationToken);
        return list;
    }

    public async Task<IReadOnlyCollection<BuildingOwner>> GetAllOwnersByBuildingId(Guid buildingId,
        CancellationToken cancellationToken = default) {
        var list = await context.BuildingOwners
            .Where(o => o.BuildingId == buildingId)
            .ToArrayAsync(cancellationToken);
        return list;
    }

    public async Task RemoveOwner(Guid buildingId, Guid ownerId, CancellationToken cancellationToken = default) {
        await context.BuildingOwners
            .Where(o => o.BuildingId == buildingId && o.OwnerId == ownerId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}