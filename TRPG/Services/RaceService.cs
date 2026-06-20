using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

public class RaceService(TrpgDbContext context)
{
    public async Task<Race?> GetById(Guid id, CancellationToken cancellationToken = default)
        => await context.Races.FindAsync([id], cancellationToken);

    public async Task<List<Race>> GetAll(CancellationToken cancellationToken = default)
        => await context.Races.ToListAsync(cancellationToken);
}
