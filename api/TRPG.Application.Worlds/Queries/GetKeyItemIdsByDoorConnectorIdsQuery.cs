using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Worlds.Queries;

public class GetKeyItemIdsByDoorConnectorIdsQuery
{
    public required IReadOnlyCollection<Guid> DoorConnectorIds { get; init; }
}

internal class GetKeyItemIdsByDoorConnectorIdsQueryHandler(IWorldsDbContext context)
    : IQueryHandler<
        GetKeyItemIdsByDoorConnectorIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>
    >
{
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> Handle(
        GetKeyItemIdsByDoorConnectorIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var keys = await context
            .DoorConnectorKeys.Where(k =>
                query.DoorConnectorIds.AsEnumerable().Contains(k.DoorConnectorId)
            )
            .ToArrayAsync(cancellationToken);

        return keys.GroupBy(k => k.DoorConnectorId)
            .ToDictionary(
                group => group.Key,
                IReadOnlyList<Guid> (group) => group.Select(k => k.ItemId).ToArray()
            );
    }
}
