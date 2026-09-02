using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Queries;

public class GetGuardAtLocationQuery
{
    public required Guid WorldId { get; init; }
    public required Guid LocationId { get; init; }
}

internal class GetGuardAtLocationQueryHandler(ICreaturesDbContext context)
    : IQueryHandler<GetGuardAtLocationQuery, Creature?>
{
    public async Task<Creature?> Handle(
        GetGuardAtLocationQuery query,
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
            .FirstOrDefaultAsync(cancellationToken);
}
