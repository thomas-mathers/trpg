using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Queries;

public class GetPropsByIdsQuery
{
    public required IReadOnlyCollection<Guid> Ids { get; init; }
}

internal class GetPropsByIdsQueryHandler(IPropsDbContext context)
    : IQueryHandler<GetPropsByIdsQuery, IReadOnlyDictionary<Guid, Prop>>
{
    public async Task<IReadOnlyDictionary<Guid, Prop>> Handle(
        GetPropsByIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Props.AsNoTracking()
            .Where(prop => query.Ids.Contains(prop.Id))
            .ToDictionaryAsync(prop => prop.Id, cancellationToken);
    }
}
