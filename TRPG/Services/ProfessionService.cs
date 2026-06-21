using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

internal class ProfessionService(TrpgDbContext context)
{
    public async Task<Profession?> GetById(Guid id, CancellationToken cancellationToken = default)
        => await context.Professions.FindAsync([id], cancellationToken);

    public async Task<ReadOnlyCollection<Profession>> GetAll(CancellationToken cancellationToken = default)
    {
        var list = await context.Professions.ToListAsync(cancellationToken);
        return list.AsReadOnly();
    }
}
