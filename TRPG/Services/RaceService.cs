using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

internal class RaceService(TrpgDbContext context)
{
    public async Task<Race?> GetById(Guid id, CancellationToken cancellationToken = default)
        => await context.Races.FindAsync([id], cancellationToken);

    public async Task<ReadOnlyCollection<Race>> GetAll(CancellationToken cancellationToken = default)
    {
        var list = await context.Races.ToListAsync(cancellationToken);
        return list.AsReadOnly();
    }
}
