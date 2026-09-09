using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Queries;

public class GetUnresolvedTrapByLocationIdQuery
{
    public required Guid LocationId { get; init; }
}

internal class GetUnresolvedTrapByLocationIdQueryHandler(IPropsDbContext context)
    : IQueryHandler<GetUnresolvedTrapByLocationIdQuery, Trigger?>
{
    public async Task<Trigger?> Handle(
        GetUnresolvedTrapByLocationIdQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Props.AsNoTracking()
            .OfType<Trigger>()
            .FirstOrDefaultAsync(
                p => p.LocationId == query.LocationId && p.TrapKind != null && !p.IsResolved,
                cancellationToken
            );
}
