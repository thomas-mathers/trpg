using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Handling;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.CreatureJobs.Queries;

public class GetWorkstationsByLocationIdQuery
{
    public required Guid LocationId { get; init; }
}

internal class GetWorkstationsByLocationIdQueryHandler(TrpgDbContext context)
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
