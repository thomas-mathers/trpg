using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

internal class WorldEventService(TrpgDbContext context)
{
    public async Task Add(WorldEvent worldEvent, CancellationToken cancellationToken = default)
    {
        context.WorldEvents.Add(worldEvent);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<WorldEvent?> GetById(Guid id, CancellationToken cancellationToken = default)
        => await context.WorldEvents.FindAsync([id], cancellationToken);

    public async Task<ReadOnlyCollection<WorldEvent>> GetAllByRegion(Circle region, CancellationToken cancellationToken = default)
    {
        var events = await context.WorldEvents
            .Where(e => e.Region.Center.WorldId == region.Center.WorldId)
            .ToListAsync(cancellationToken);

        return events
            .Where(e => Distance(e.Region.Center.Coordinates, region.Center.Coordinates) <= region.Radius)
            .ToList().AsReadOnly();
    }

    private static float Distance(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}
