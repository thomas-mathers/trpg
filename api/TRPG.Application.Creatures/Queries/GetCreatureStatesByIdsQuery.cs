using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Queries;

public class GetCreatureStatesByIdsQuery
{
    public required IReadOnlyCollection<Guid> Ids { get; init; }
}

internal class GetCreatureStatesByIdsQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetCreatureStatesByIdsQuery, IReadOnlyDictionary<Guid, CreatureState>>
{
    public async Task<IReadOnlyDictionary<Guid, CreatureState>> Handle(
        GetCreatureStatesByIdsQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Creatures.AsNoTracking()
            .Where(creature => query.Ids.AsEnumerable().Contains(creature.Id))
            .ToDictionaryAsync(
                creature => creature.Id,
                creature => creature.State,
                cancellationToken
            );
}
