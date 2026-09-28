using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Queries;

public class GetSeatIdsByOccupantIdsQuery
{
    public required IReadOnlyCollection<Guid> OccupantIds { get; init; }
}

internal class GetSeatIdsByOccupantIdsQueryHandler(IPropsDbContext context)
    : IQueryHandler<GetSeatIdsByOccupantIdsQuery, IReadOnlyDictionary<Guid, Guid>>
{
    public async Task<IReadOnlyDictionary<Guid, Guid>> Handle(
        GetSeatIdsByOccupantIdsQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Props.OfType<Seat>()
            .AsNoTracking()
            .Where(seat => query.OccupantIds.AsEnumerable().Contains(seat.OccupantId ?? Guid.Empty))
            .ToDictionaryAsync(seat => seat.OccupantId!.Value, seat => seat.Id, cancellationToken);
}
