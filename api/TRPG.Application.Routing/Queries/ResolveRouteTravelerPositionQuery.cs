using TRPG.Application.Common.Queries;
using TRPG.Domain;

namespace TRPG.Application.Routing.Queries;

public class ResolveRouteTravelerPositionQuery
{
    public required Guid RouteTravelerId { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class ResolveRouteTravelerPositionQueryHandler(
    IQueryHandler<
        ResolveRouteTravelerPositionsQuery,
        IReadOnlyDictionary<Guid, ResolvedRouteTravelerPosition>
    > resolveRouteTravelerPositions
) : IQueryHandler<ResolveRouteTravelerPositionQuery, RouteTimelinePosition?>
{
    public async Task<RouteTimelinePosition?> Handle(
        ResolveRouteTravelerPositionQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var positions = await resolveRouteTravelerPositions.Handle(
            new ResolveRouteTravelerPositionsQuery
            {
                RouteTravelerIds = [query.RouteTravelerId],
                GameTime = query.GameTime,
            },
            cancellationToken
        );
        return positions.GetValueOrDefault(query.RouteTravelerId)?.Position;
    }
}
