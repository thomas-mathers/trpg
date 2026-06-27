using System.Collections.ObjectModel;
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

    public async Task<ReadOnlyCollection<Building>> GetAllByCityId(Guid cityId,
        CancellationToken cancellationToken = default) {
        var list = await context.Buildings
            .Where(b => b.CityId == cityId)
            .ToListAsync(cancellationToken);
        return list.AsReadOnly();
    }

    public async Task<ReadOnlyCollection<Prop>> GetAllPropsByRoomId(Guid roomId,
        CancellationToken cancellationToken = default) {
        var list = await context.Props
            .Where(p => p.RoomId == roomId)
            .ToListAsync(cancellationToken);
        return list.AsReadOnly();
    }

    public async Task<ReadOnlyCollection<BuildingOwner>> GetAllOwnersByBuildingId(Guid buildingId,
        CancellationToken cancellationToken = default) {
        var list = await context.BuildingOwners
            .Where(o => o.BuildingId == buildingId)
            .ToListAsync(cancellationToken);
        return list.AsReadOnly();
    }

    public async Task RemoveOwner(Guid buildingId, Guid ownerId, CancellationToken cancellationToken = default) {
        await context.BuildingOwners
            .Where(o => o.BuildingId == buildingId && o.OwnerId == ownerId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}