using Microsoft.Extensions.Caching.Memory;

namespace TRPG.Application.Narration.Queries;

public class GetLoreAnchorAutomatonByWorldQuery
{
    public required Guid WorldId { get; init; }
}

public class GetLoreAnchorAutomatonByWorldQueryHandler(
    GetLoreAnchorsByWorldQueryHandler getLoreAnchorsByWorld,
    IMemoryCache cache
)
{
    public static string CacheKey(Guid worldId) => $"namedEntityAutomaton:{worldId}";

    public async Task<LoreAnchorAutomaton> Handle(
        GetLoreAnchorAutomatonByWorldQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var entities = await getLoreAnchorsByWorld.Handle(
            new GetLoreAnchorsByWorldQuery { WorldId = query.WorldId },
            cancellationToken
        );

        return cache.GetOrCreate(
            CacheKey(query.WorldId),
            _ => LoreAnchorAutomaton.Build(entities)
        )!;
    }
}
