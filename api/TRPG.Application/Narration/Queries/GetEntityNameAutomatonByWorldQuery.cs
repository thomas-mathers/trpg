using Microsoft.Extensions.Caching.Memory;
using TRPG.Application.Scenes.Queries;

namespace TRPG.Application.Narration.Queries;

internal class GetEntityNameAutomatonByWorldQuery
{
    public required Guid WorldId { get; init; }
}

internal class GetEntityNameAutomatonByWorldQueryHandler(
    GetLoreAnchorsByWorldQueryHandler getLoreAnchorsByWorld,
    IMemoryCache cache
)
{
    public static string CacheKey(Guid worldId) => $"namedEntityAutomaton:{worldId}";

    public async Task<EntityNameAutomaton> Handle(
        GetEntityNameAutomatonByWorldQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var entities = await getLoreAnchorsByWorld.Handle(
            new GetLoreAnchorsByWorldQuery { WorldId = query.WorldId },
            cancellationToken
        );

        return cache.GetOrCreate(
            CacheKey(query.WorldId),
            _ => EntityNameAutomaton.Build(entities)
        )!;
    }
}
