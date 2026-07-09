using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Buildings.Queries;

internal class GetRoomsByIdsQuery
{
    public required IReadOnlyCollection<Guid> RoomIds { get; init; }
}

internal class GetRoomsByIdsQueryHandler(TrpgDbContext context, IMemoryCache cache)
{
    public async Task<IReadOnlyDictionary<Guid, Room>> Handle(
        GetRoomsByIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var result = new Dictionary<Guid, Room>();
        var missingIds = new List<Guid>();
        foreach (var id in query.RoomIds)
        {
            if (cache.TryGetValue($"room:{id}", out Room? room) && room != null)
            {
                result[id] = room;
            }
            else
            {
                missingIds.Add(id);
            }
        }

        if (missingIds.Count > 0)
        {
            var fetched = await context
                .Rooms.AsNoTracking()
                .Where(r => missingIds.Contains(r.Id))
                .ToArrayAsync(cancellationToken);
            foreach (var room in fetched)
            {
                cache.Set($"room:{room.Id}", room);
                result[room.Id] = room;
            }
        }

        return result;
    }
}
