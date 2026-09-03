using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Queries;

public class GetBedByLocationIdQuery
{
    public required Guid LocationId { get; init; }
}

internal class GetBedByLocationIdQueryHandler(IPropsDbContext context)
    : IQueryHandler<GetBedByLocationIdQuery, Bed?>
{
    public async Task<Bed?> Handle(
        GetBedByLocationIdQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Props.AsNoTracking()
            .OfType<Bed>()
            .FirstOrDefaultAsync(p => p.LocationId == query.LocationId, cancellationToken);
}
