using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Queries;

public class GetAvailableSeatsByLocationIdQuery
{
    public required Guid LocationId { get; init; }
}

internal class GetAvailableSeatsByLocationIdQueryHandler(IPropsDbContext context)
    : IQueryHandler<GetAvailableSeatsByLocationIdQuery, IReadOnlyList<Seat>>
{
    public async Task<IReadOnlyList<Seat>> Handle(
        GetAvailableSeatsByLocationIdQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Props.OfType<Seat>()
            .AsNoTracking()
            .Where(seat => seat.LocationId == query.LocationId && seat.OccupantId == null)
            .OrderBy(seat => seat.Id)
            .ToArrayAsync(cancellationToken);
}
