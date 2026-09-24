using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Queries;

public class GetSeatOccupantsByIdsQuery
{
    public required IReadOnlyCollection<Guid> SeatIds { get; init; }
}

internal class GetSeatOccupantsByIdsQueryHandler(IPropsDbContext context)
    : IQueryHandler<GetSeatOccupantsByIdsQuery, IReadOnlyDictionary<Guid, Guid?>>
{
    public async Task<IReadOnlyDictionary<Guid, Guid?>> Handle(
        GetSeatOccupantsByIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        if (query.SeatIds.Count == 0)
        {
            return new Dictionary<Guid, Guid?>();
        }

        return await context
            .Props.OfType<Seat>()
            .AsNoTracking()
            .Where(seat => query.SeatIds.AsEnumerable().Contains(seat.Id))
            .ToDictionaryAsync(seat => seat.Id, seat => seat.OccupantId, cancellationToken);
    }
}
