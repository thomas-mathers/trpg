using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Queries;

public class GetWorkstationsByLocationIdQuery
{
    public required Guid LocationId { get; init; }
}

internal class GetWorkstationsByLocationIdQueryHandler(IPropsDbContext context)
    : IQueryHandler<GetWorkstationsByLocationIdQuery, IReadOnlyCollection<Workstation>>
{
    public async Task<IReadOnlyCollection<Workstation>> Handle(
        GetWorkstationsByLocationIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Props.AsNoTracking()
            .Where(p => p.LocationId == query.LocationId)
            .OfType<Workstation>()
            .ToArrayAsync(cancellationToken);
    }
}
