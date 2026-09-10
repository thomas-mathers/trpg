using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Queries;

public class GetPulledLeverIdsQuery
{
    public required Guid WorldId { get; init; }
}

internal class GetPulledLeverIdsQueryHandler(IPropsDbContext context)
    : IQueryHandler<GetPulledLeverIdsQuery, IReadOnlySet<Guid>>
{
    public async Task<IReadOnlySet<Guid>> Handle(
        GetPulledLeverIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Props.AsNoTracking()
            .OfType<Lever>()
            .Where(lever => lever.WorldId == query.WorldId && lever.IsPulled)
            .Select(lever => lever.Id)
            .ToHashSetAsync(cancellationToken);
    }
}
