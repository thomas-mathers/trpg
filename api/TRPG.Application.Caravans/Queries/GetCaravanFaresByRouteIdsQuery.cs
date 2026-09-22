using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Caravans.Queries;

public class GetCaravanFaresByRouteIdsQuery
{
    public required IReadOnlyCollection<Guid> RouteIds { get; init; }
}

internal class GetCaravanFaresByRouteIdsQueryHandler(ICaravansDbContext context)
    : IQueryHandler<GetCaravanFaresByRouteIdsQuery, IReadOnlyDictionary<Guid, CaravanFare>>
{
    public async Task<IReadOnlyDictionary<Guid, CaravanFare>> Handle(
        GetCaravanFaresByRouteIdsQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .CaravanFares.AsNoTracking()
            .Where(f => query.RouteIds.AsEnumerable().Contains(f.RouteId))
            .ToDictionaryAsync(f => f.RouteId, cancellationToken);
}
