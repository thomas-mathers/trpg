using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

internal class JobService(TrpgDbContext context)
{
    public async Task Add(Job job, CancellationToken cancellationToken = default)
    {
        context.Jobs.Add(job);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ReadOnlyCollection<Job>> GetAllByPersonId(Guid personId, CancellationToken cancellationToken = default)
    {
        var list = await context.Jobs
            .Where(j => j.PersonId == personId)
            .OrderByDescending(j => j.Priority)
            .ToListAsync(cancellationToken);
        return list.AsReadOnly();
    }

    public async Task Update(Job job, CancellationToken cancellationToken = default)
    {
        context.Jobs.Update(job);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task Delete(Guid id, CancellationToken cancellationToken = default)
        => await context.Jobs.Where(j => j.Id == id).ExecuteDeleteAsync(cancellationToken);
}
