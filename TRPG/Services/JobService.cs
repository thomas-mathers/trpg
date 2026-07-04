using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

internal class JobService(TrpgDbContext context) {
    public async Task Add(Job job, CancellationToken cancellationToken = default) {
        context.Jobs.Add(job);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Job>> GetAllByPersonId(Guid personId,
        CancellationToken cancellationToken = default) {
        var list = await context.Jobs
            .Where(j => j.PersonId == personId)
            .OrderByDescending(j => j.Priority)
            .ToArrayAsync(cancellationToken);
        return list;
    }

    public async Task<IReadOnlyList<Guid>> GetPersonIdsByRoomId(Guid roomId, CancellationToken cancellationToken = default) {
        var ids = await context.Jobs.Where(j => j.RoomId == roomId).Select(j => j.PersonId).Distinct().ToArrayAsync(cancellationToken);
        return ids;
    }

    public async Task Update(Job job, CancellationToken cancellationToken = default) {
        context.Jobs.Update(job);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task Delete(Guid id, CancellationToken cancellationToken = default) {
        await context.Jobs.Where(j => j.Id == id).ExecuteDeleteAsync(cancellationToken);
    }
}