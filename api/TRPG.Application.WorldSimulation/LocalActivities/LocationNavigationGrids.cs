using Microsoft.Extensions.Caching.Memory;
using TRPG.Application.Common.Navigation;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;

namespace TRPG.Application.WorldSimulation.LocalActivities;

internal sealed class LocationNavigationGrids(
    IMemoryCache cache,
    IQueryHandler<GetMovementSpaceQuery, MovementSpace?> getMovementSpace
)
{
    private static readonly TimeSpan SlidingExpiration = TimeSpan.FromMinutes(10);

    public async Task<NavigationGrid> Get(Guid locationId, CancellationToken cancellationToken)
    {
        var grid = await cache.GetOrCreateAsync(
            NavigationCacheKeys.Grid(locationId),
            async entry =>
            {
                entry.SlidingExpiration = SlidingExpiration;
                var space =
                    await getMovementSpace.Handle(
                        new GetMovementSpaceQuery { LocationId = locationId },
                        cancellationToken
                    )
                    ?? throw new InvalidOperationException(
                        $"Location {locationId} has no movement space."
                    );

                return NavigationGrid.From(space);
            }
        );

        return grid!;
    }
}
