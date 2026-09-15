using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Queries;

public class GetContainerIdsByLocationsQuery
{
    public required IReadOnlyCollection<Guid> LocationIds { get; init; }
}

internal class GetContainerIdsByLocationsQueryHandler(IPropsDbContext context)
    : IQueryHandler<GetContainerIdsByLocationsQuery, IReadOnlyList<Guid>>
{
    public async Task<IReadOnlyList<Guid>> Handle(
        GetContainerIdsByLocationsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Props.AsNoTracking()
            .OfType<Container>()
            .Where(container => query.LocationIds.AsEnumerable().Contains(container.LocationId))
            .Select(container => container.Id)
            .ToArrayAsync(cancellationToken);
    }
}
