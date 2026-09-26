using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Routing.Queries;

public record RouteTravelerJourney(string Purpose, string NextDestination);

public class GetRouteTravelerJourneysByCreatureIdsQuery
{
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class GetRouteTravelerJourneysByCreatureIdsQueryHandler(
    IRoutingDbContext context,
    IQueryHandler<
        ResolveRouteTravelerPositionsQuery,
        IReadOnlyDictionary<Guid, ResolvedRouteTravelerPosition>
    > resolvePositions,
    IQueryHandler<GetLocationsByIdsQuery, IReadOnlyDictionary<Guid, Location>> getLocationsByIds
)
    : IQueryHandler<
        GetRouteTravelerJourneysByCreatureIdsQuery,
        IReadOnlyDictionary<Guid, RouteTravelerJourney>
    >
{
    public async Task<IReadOnlyDictionary<Guid, RouteTravelerJourney>> Handle(
        GetRouteTravelerJourneysByCreatureIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var memberships = await context
            .RouteTravelerMembers.AsNoTracking()
            .Where(member => query.CreatureIds.AsEnumerable().Contains(member.CreatureId))
            .ToArrayAsync(cancellationToken);
        if (memberships.Length == 0)
        {
            return new Dictionary<Guid, RouteTravelerJourney>();
        }

        var travelerIds = memberships.Select(member => member.RouteTravelerId).Distinct().ToArray();
        var travelers = await context
            .RouteTravelers.AsNoTracking()
            .Where(traveler => travelerIds.AsEnumerable().Contains(traveler.Id))
            .ToDictionaryAsync(traveler => traveler.Id, cancellationToken);
        var positions = await resolvePositions.Handle(
            new ResolveRouteTravelerPositionsQuery
            {
                RouteTravelerIds = travelerIds,
                GameTime = query.GameTime,
            },
            cancellationToken
        );
        var locations = await getLocationsByIds.Handle(
            new GetLocationsByIdsQuery
            {
                Ids = positions
                    .Values.Select(position => position.NextLocationId)
                    .Distinct()
                    .ToArray(),
            },
            cancellationToken
        );

        return memberships
            .Where(member =>
                travelers.GetValueOrDefault(member.RouteTravelerId)?.Purpose != null
                && positions.ContainsKey(member.RouteTravelerId)
            )
            .ToDictionary(
                member => member.CreatureId,
                member =>
                {
                    var traveler = travelers[member.RouteTravelerId];
                    var nextLocationId = positions[member.RouteTravelerId].NextLocationId;
                    return new RouteTravelerJourney(
                        traveler.Purpose!,
                        locations.GetValueOrDefault(nextLocationId)?.Name ?? "Unknown"
                    );
                }
            );
    }
}
