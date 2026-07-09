using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Creatures.Queries;

internal class GetAllCreaturesInStateQuery
{
    public required Guid WorldId { get; init; }
    public required Guid StateId { get; init; }
}

internal class GetAllCreaturesInStateQueryHandler(TrpgDbContext context)
{
    public async Task<IReadOnlyCollection<Creature>> Handle(
        GetAllCreaturesInStateQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Creatures.AsNoTracking()
            .Where(p => p.WorldId == query.WorldId && p.StateId == query.StateId)
            .ToArrayAsync(cancellationToken);
    }
}
