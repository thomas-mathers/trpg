using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Navigation;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Worlds.Queries;

public class GetNearestReachableLocationQuery
{
    public required Guid WorldId { get; init; }
    public required Guid FromLocationId { get; init; }
    public required IReadOnlyCollection<Guid> CandidateLocationIds { get; init; }
}

internal class GetNearestReachableLocationQueryHandler(
    IWorldsDbContext context,
    IQueryHandler<GetTravelGraphQuery, TravelGraph> getTravelGraph
) : IQueryHandler<GetNearestReachableLocationQuery, Guid?>
{
    public async Task<Guid?> Handle(
        GetNearestReachableLocationQuery query,
        CancellationToken cancellationToken = default
    )
    {
        if (query.CandidateLocationIds.Count == 0)
        {
            return null;
        }

        var fromAnchor = await context
            .Locations.AsNoTracking()
            .Where(location => location.Id == query.FromLocationId)
            .Select(location => location.CoarseAnchorLocationId)
            .FirstOrDefaultAsync(cancellationToken);

        var candidateAnchors = await context
            .Locations.AsNoTracking()
            .Where(location => query.CandidateLocationIds.Contains(location.Id))
            .Select(location => new { location.Id, location.CoarseAnchorLocationId })
            .ToArrayAsync(cancellationToken);

        var candidateIdByAnchor = candidateAnchors
            .GroupBy(candidate => candidate.CoarseAnchorLocationId)
            .ToDictionary(group => group.Key, group => group.First().Id);

        if (candidateIdByAnchor.Count == 0)
        {
            return null;
        }

        var graph = await getTravelGraph.Handle(
            new GetTravelGraphQuery { WorldId = query.WorldId },
            cancellationToken
        );
        var nearestAnchor = graph.FindNearestLocation(
            fromAnchor,
            candidateIdByAnchor.Keys.ToHashSet()
        );

        return nearestAnchor is null ? null : candidateIdByAnchor[nearestAnchor.Value];
    }
}
