using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Worlds.Queries;

public class GetLocationIdsByStateIdQuery
{
    public required Guid StateId { get; init; }
}

internal class GetLocationIdsByStateIdQueryHandler(IWorldsDbContext context)
    : IQueryHandler<GetLocationIdsByStateIdQuery, IReadOnlyCollection<Guid>>
{
    public async Task<IReadOnlyCollection<Guid>> Handle(
        GetLocationIdsByStateIdQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Locations.AsNoTracking()
            .Where(location => location.StateId == query.StateId)
            .Select(location => location.Id)
            .ToArrayAsync(cancellationToken);
}
