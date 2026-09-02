using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Queries;

public class GetPropByIdQuery
{
    public required Guid Id { get; init; }
}

internal class GetPropByIdQueryHandler(IPropsDbContext context)
    : IQueryHandler<GetPropByIdQuery, Prop?>
{
    public async Task<Prop?> Handle(
        GetPropByIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Props.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == query.Id, cancellationToken);
    }
}
