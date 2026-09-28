using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Caravans.Queries;

public class GetCaravanTicketsByCaravanIdsQuery
{
    public required Guid CreatureId { get; init; }
    public required IReadOnlyCollection<Guid> CaravanIds { get; init; }
}

internal class GetCaravanTicketsByCaravanIdsQueryHandler(ICaravansDbContext context)
    : IQueryHandler<GetCaravanTicketsByCaravanIdsQuery, IReadOnlyDictionary<Guid, CaravanTicket>>
{
    public async Task<IReadOnlyDictionary<Guid, CaravanTicket>> Handle(
        GetCaravanTicketsByCaravanIdsQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .CaravanTickets.AsNoTracking()
            .Where(t =>
                t.CreatureId == query.CreatureId
                && query.CaravanIds.AsEnumerable().Contains(t.RouteTravelerId)
            )
            .ToDictionaryAsync(t => t.RouteTravelerId, cancellationToken);
}
