using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Worlds.Queries;

public class GetLeverIdsByDoorConnectorIdsQuery
{
    public required IReadOnlyCollection<Guid> DoorConnectorIds { get; init; }
}

internal class GetLeverIdsByDoorConnectorIdsQueryHandler(IWorldsDbContext context)
    : IQueryHandler<
        GetLeverIdsByDoorConnectorIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>
    >
{
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> Handle(
        GetLeverIdsByDoorConnectorIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var levers = await context
            .DoorConnectorLevers.Where(l =>
                query.DoorConnectorIds.AsEnumerable().Contains(l.DoorConnectorId)
            )
            .ToArrayAsync(cancellationToken);

        return levers
            .GroupBy(l => l.DoorConnectorId)
            .ToDictionary(
                group => group.Key,
                IReadOnlyList<Guid> (group) => group.Select(l => l.LeverId).ToArray()
            );
    }
}
