using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Queries;

public class GetCellLocationIdsQuery
{
    public required Guid WorldId { get; init; }
}

internal class GetCellLocationIdsQueryHandler(IPropsDbContext context)
    : IQueryHandler<GetCellLocationIdsQuery, IReadOnlySet<Guid>>
{
    public async Task<IReadOnlySet<Guid>> Handle(
        GetCellLocationIdsQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Props.AsNoTracking()
            .OfType<Cell>()
            .Where(cell => cell.WorldId == query.WorldId)
            .Select(cell => cell.LocationId)
            .ToHashSetAsync(cancellationToken);
}
