using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Buildings.Queries;

internal class GetWorkstationsByRoomIdQuery
{
    public required Guid RoomId { get; init; }
}

internal class GetWorkstationsByRoomIdQueryHandler(TrpgDbContext context)
{
    // Deliberately uncached, like GetConnectorsByRoomIdQuery — OccupantId is mutated at runtime by
    // SetWorkstationOccupantCommand.
    public async Task<IReadOnlyCollection<Workstation>> Handle(
        GetWorkstationsByRoomIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Props.AsNoTracking()
            .Where(p => p.RoomId == query.RoomId)
            .OfType<Workstation>()
            .ToArrayAsync(cancellationToken);
    }
}
