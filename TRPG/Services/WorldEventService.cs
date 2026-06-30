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

    public async Task<IReadOnlyCollection<WorldEvent>> GetAllByRegion(Guid worldId, Circle region,
        CancellationToken cancellationToken = default) {
        var events = await context.WorldEvents
            .Where(e => e.WorldId == worldId)
            .ToArrayAsync(cancellationToken);

        return events
            .Where(e => Distance(e.Region.Center.Coordinates, region.Center.Coordinates) <= region.Radius)
            .ToArray();
    }

    private static double Distance(Point a, Point b) {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}