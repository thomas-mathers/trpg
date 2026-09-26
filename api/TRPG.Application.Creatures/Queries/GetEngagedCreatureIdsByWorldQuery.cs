using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Creatures.Queries;

public class GetEngagedCreatureIdsByWorldQuery
{
    public required Guid WorldId { get; init; }
}

internal class GetEngagedCreatureIdsByWorldQueryHandler(ICreaturesDbContext context)
    : IQueryHandler<GetEngagedCreatureIdsByWorldQuery, IReadOnlyCollection<Guid>>
{
    public async Task<IReadOnlyCollection<Guid>> Handle(
        GetEngagedCreatureIdsByWorldQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Creatures.AsNoTracking()
            .Where(creature => creature.WorldId == query.WorldId && creature.IsEngaged)
            .Select(creature => creature.Id)
            .ToArrayAsync(cancellationToken);
}
