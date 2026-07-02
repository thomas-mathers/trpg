using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

internal class WorldEventService(TrpgDbContext context) {
    public async Task Add(WorldEvent worldEvent, CancellationToken cancellationToken = default) {
        context.WorldEvents.Add(worldEvent);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<WorldEvent?> GetById(Guid id, CancellationToken cancellationToken = default) {
        return await context.WorldEvents.FindAsync([id], cancellationToken);
    }

    public async Task<IReadOnlyCollection<WorldEvent>> GetAllByRegion(Guid worldId, Guid regionId,
        CancellationToken cancellationToken = default) {
        return await context.WorldEvents
            .Where(e => e.WorldId == worldId && e.RegionId == regionId)
            .ToArrayAsync(cancellationToken);
    }
}