using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

public class ProfessionService(TrpgDbContext context)
{
    public async Task<Profession?> GetById(Guid id, CancellationToken cancellationToken = default)
        => await context.Professions.FindAsync([id], cancellationToken);

    public async Task<List<Profession>> GetAll(CancellationToken cancellationToken = default)
        => await context.Professions.ToListAsync(cancellationToken);
}
