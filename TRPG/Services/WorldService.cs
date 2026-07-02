using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

internal class WorldService(TrpgDbContext context) {
    public async Task<World?> GetWorld(Guid worldId, CancellationToken cancellationToken) {
        return await context.Worlds.FirstOrDefaultAsync(w => w.Id == worldId, cancellationToken);
    }
}