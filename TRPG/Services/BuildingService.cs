using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

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

    public async Task<IReadOnlyCollection<Building>> GetAllByRegionId(Guid regionId,
        CancellationToken cancellationToken = default) {
        var list = await context.Buildings
            .Where(b => b.RegionId == regionId)
            .ToArrayAsync(cancellationToken);
        return list;
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