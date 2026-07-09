using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Services;

internal class WorldService(TrpgDbContext context)
{
    public async Task<World?> GetWorld(Guid worldId, CancellationToken cancellationToken)
    {
        return await context
            .Worlds.AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == worldId, cancellationToken);
    }

    public async Task Update(World world, CancellationToken cancellationToken = default)
    {
        context.Worlds.Update(world);
        await context.SaveChangesAsync(cancellationToken);
    }
}
