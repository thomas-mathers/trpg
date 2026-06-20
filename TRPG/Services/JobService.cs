using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

public class JobService(TrpgDbContext context)
{
    public async Task Add(Job job, CancellationToken cancellationToken = default)
    {
        context.Jobs.Add(job);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<Job>> GetAllByPersonId(Guid personId, CancellationToken cancellationToken = default)
        => await context.Jobs
            .Where(j => j.PersonId == personId)
            .OrderByDescending(j => j.Priority)
            .ToListAsync(cancellationToken);

    public async Task Update(Job job, CancellationToken cancellationToken = default)
    {
        context.Jobs.Update(job);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task Delete(Guid id, CancellationToken cancellationToken = default)
        => await context.Jobs.Where(j => j.Id == id).ExecuteDeleteAsync(cancellationToken);
}
