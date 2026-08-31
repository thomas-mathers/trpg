using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetRoomsByIdsQuery
{
    public required IReadOnlyCollection<Guid> Ids { get; init; }
}

internal class GetRoomsByIdsQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetRoomsByIdsQuery, IReadOnlyDictionary<Guid, Room>>
{
    public async Task<IReadOnlyDictionary<Guid, Room>> Handle(
        GetRoomsByIdsQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Rooms.AsNoTracking()
            .Where(room => query.Ids.AsEnumerable().Contains(room.Id))
            .ToDictionaryAsync(room => room.Id, cancellationToken);
}
