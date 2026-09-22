using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Caravans.Queries;

public class GetCaravanTicketQuery
{
    public required Guid CreatureId { get; init; }
    public required Guid CaravanId { get; init; }
}

internal class GetCaravanTicketQueryHandler(ICaravansDbContext context)
    : IQueryHandler<GetCaravanTicketQuery, CaravanTicket?>
{
    public Task<CaravanTicket?> Handle(
        GetCaravanTicketQuery query,
        CancellationToken cancellationToken = default
    ) =>
        context
            .CaravanTickets.AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.CreatureId == query.CreatureId && t.RouteTravelerId == query.CaravanId,
                cancellationToken
            );
}
