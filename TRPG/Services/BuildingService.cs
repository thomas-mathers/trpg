using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

public class BuildingService(TrpgDbContext context)
{
    public async Task AddOwner(Guid buildingId, Guid ownerId, CancellationToken cancellationToken = default)
    {
        context.BuildingOwners.Add(new BuildingOwner
        {
            Id = Guid.NewGuid(),
            BuildingId = buildingId,
            OwnerId = ownerId
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Building?> GetById(Guid id, CancellationToken cancellationToken = default)
        => await context.Buildings.FindAsync([id], cancellationToken);

    public async Task<List<Building>> GetAllByCityId(Guid cityId, CancellationToken cancellationToken = default)
        => await context.Buildings
            .Where(b => b.CityId == cityId)
            .ToListAsync(cancellationToken);

    public async Task<List<BuildingProp>> GetAllPropsByBuildingId(Guid buildingId, CancellationToken cancellationToken = default)
        => await context.BuildingProps
            .Where(p => p.BuildingId == buildingId)
            .ToListAsync(cancellationToken);

    public async Task<List<BuildingOwner>> GetAllOwnersByBuildingId(Guid buildingId, CancellationToken cancellationToken = default)
        => await context.BuildingOwners
            .Where(o => o.BuildingId == buildingId)
            .ToListAsync(cancellationToken);

    public async Task RemoveOwner(Guid buildingId, Guid ownerId, CancellationToken cancellationToken = default)
        => await context.BuildingOwners
            .Where(o => o.BuildingId == buildingId && o.OwnerId == ownerId)
            .ExecuteDeleteAsync(cancellationToken);
}
