using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Queries;

public class GetGuardsAtLocationQuery
{
    public required Guid WorldId { get; init; }
    public required Guid LocationId { get; init; }
}

internal class GetGuardsAtLocationQueryHandler(ICreaturesDbContext context)
    : IQueryHandler<GetGuardsAtLocationQuery, IReadOnlyList<Creature>>
{
    public async Task<IReadOnlyList<Creature>> Handle(
        GetGuardsAtLocationQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Creatures.AsNoTracking()
            .Where(creature =>
                creature.WorldId == query.WorldId
                && creature.LocationId == query.LocationId
                && creature.Profession == Profession.Guard
                && creature.State != CreatureState.Dead
            )
            .ToArrayAsync(cancellationToken);
}
