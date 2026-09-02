using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Queries;

public class GetLivingCreatureCountBySpawnerIdQuery
{
    public required Guid SpawnerId { get; init; }
}

internal class GetLivingCreatureCountBySpawnerIdQueryHandler(ICreaturesDbContext context)
    : IQueryHandler<GetLivingCreatureCountBySpawnerIdQuery, int>
{
    public async Task<int> Handle(
        GetLivingCreatureCountBySpawnerIdQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context.Creatures.CountAsync(
            creature =>
                creature.SpawnerId == query.SpawnerId && creature.State != CreatureState.Dead,
            cancellationToken
        );
}
