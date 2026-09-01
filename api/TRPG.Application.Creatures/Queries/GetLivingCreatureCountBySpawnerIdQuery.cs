using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Queries;

public class GetLivingCreatureCountBySpawnerIdQuery
{
    public required Guid SpawnerId { get; init; }
}

internal class GetLivingCreatureCountBySpawnerIdQueryHandler(TrpgDbContext context)
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
