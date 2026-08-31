using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Algorithms;
using TRPG.Application.Common.Queries;
using TRPG.Data;

namespace TRPG.Application.Locations.Queries;

public class GetNearestReachableLocationQuery
{
    public required Guid WorldId { get; init; }
    public required Guid FromLocationId { get; init; }
    public required IReadOnlyCollection<Guid> CandidateLocationIds { get; init; }
}

internal class GetNearestReachableLocationQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetNearestReachableLocationQuery, Guid?>
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

        var edges = await context
            .LocationConnectors.AsNoTracking()
            .Where(connector => connector.WorldId == query.WorldId)
            .Join(
                context
                    .TravelConnectors.AsNoTracking()
                    .Where(travel => travel.WorldId == query.WorldId),
                connector => connector.Id,
                travel => travel.ConnectorId,
                (connector, travel) =>
                    new
                    {
                        connector.OriginLocationId,
                        connector.DestinationLocationId,
                        travel.Distance,
                    }
            )
            .ToArrayAsync(cancellationToken);

        var neighborsByOrigin = edges
            .GroupBy(edge => edge.OriginLocationId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(edge => edge.DestinationLocationId).ToArray()
            );

        var costByEdge = edges.ToDictionary(
            edge => (edge.OriginLocationId, edge.DestinationLocationId),
            edge => edge.Distance
        );

        var path = Graphs.ShortestPathToNearest(
            fromAnchor,
            candidateIdByAnchor.Keys.ToHashSet(),
            locationId => neighborsByOrigin.GetValueOrDefault(locationId, []),
            (from, to) => costByEdge[(from, to)]
        );

        return path.Count == 0 ? null : candidateIdByAnchor[path[^1]];
    }
}
