using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

internal class FactionService(TrpgDbContext context, IMemoryCache cache) {
    public async Task<Faction?> GetById(Guid id, CancellationToken cancellationToken = default) {
        return await cache.GetOrCreateAsync($"faction:{id}",
            _ => context.Factions.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, cancellationToken));
    }
}
