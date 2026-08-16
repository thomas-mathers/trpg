using Microsoft.Extensions.Caching.Memory;
using TRPG.Application.Common.Commands;
using TRPG.Application.Narration.Queries;

namespace TRPG.Application.Narration.Commands;

public class InvalidateWorldLoreAnchorsCommand
{
    public required Guid WorldId { get; init; }
}

internal class InvalidateWorldLoreAnchorsCommandHandler(IMemoryCache cache)
    : ICommandHandler<InvalidateWorldLoreAnchorsCommand>
{
    public Task Handle(
        InvalidateWorldLoreAnchorsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        cache.Remove(GetLoreAnchorsByWorldQueryHandler.CacheKey(command.WorldId));
        cache.Remove(GetLoreAnchorAutomatonByWorldQueryHandler.CacheKey(command.WorldId));
        return Task.CompletedTask;
    }
}
