using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Buildings.Queries;

internal class GetStaticPropsByRoomIdQuery
{
    public required Guid RoomId { get; init; }
}

internal class GetStaticPropsByRoomIdQueryHandler(TrpgDbContext context, IMemoryCache cache)
{
    public async Task<IReadOnlyCollection<Prop>> Handle(
        GetStaticPropsByRoomIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var props = await cache.GetOrCreateAsync<Prop[]>(
            $"staticProps:{query.RoomId}",
            async _ =>
                (
                    await context
                        .Props.AsNoTracking()
                        .Where(p => p.RoomId == query.RoomId)
                        .ToArrayAsync(cancellationToken)
                )
                    .Where(p => p is not RoomConnector)
                    .ToArray()
        );
        return props ?? [];
    }
}
