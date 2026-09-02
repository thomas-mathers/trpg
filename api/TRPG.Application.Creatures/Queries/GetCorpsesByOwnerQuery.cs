using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Queries;

public class GetCorpsesByOwnerQuery
{
    public required Guid WorldId { get; init; }
    public required Guid OwnerId { get; init; }
}

internal class GetCorpsesByOwnerQueryHandler(ICreaturesDbContext context)
    : IQueryHandler<GetCorpsesByOwnerQuery, IReadOnlyCollection<Creature>>
{
    public async Task<IReadOnlyCollection<Creature>> Handle(
        GetCorpsesByOwnerQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Creatures.AsNoTracking()
            .Where(creature =>
                creature.WorldId == query.WorldId
                && creature.PlayerCorpseOwnerId == query.OwnerId
                && creature.State == CreatureState.Dead
            )
            .ToArrayAsync(cancellationToken);
}
