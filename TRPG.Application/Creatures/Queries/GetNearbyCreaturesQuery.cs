using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Creatures.Queries;

// Anchors the location lookup on another creature (typically the player) instead of an explicit
// CreatureLocation, resolving "where is the anchor" and "who's there" in one round trip via a
// self-referencing correlated subquery, rather than fetching the anchor's row first.
internal class GetNearbyCreaturesQuery
{
    public required Guid PlayerId { get; init; }
    public Guid? ExcludingCreatureId { get; init; }
    public IReadOnlyCollection<CreatureType>? CreatureTypes { get; init; }
    public bool IncludeDead { get; init; } = true;
}

internal class GetNearbyCreaturesQueryHandler(TrpgDbContext context)
{
    public async Task<IReadOnlyCollection<CreatureSummary>> Handle(
        GetNearbyCreaturesQuery query,
        CancellationToken cancellationToken = default
    )
    {
        // Mirrors GetCreaturesAtLocationQueryHandler's room-vs-outdoor branch, just expressed
        // against the anchor's own row instead of explicit scalars - matching by RoomId alone when
        // the anchor is indoors (never additionally checking DistrictId, see
        // CreatureLocationFiltering's comment), or by DistrictId with RoomId null when outdoors.
        var creatureQuery = context
            .Creatures.AsNoTracking()
            .Where(c =>
                context.Creatures.Any(p =>
                    p.Id == query.PlayerId
                    && p.WorldId == c.WorldId
                    && p.StateId == c.StateId
                    && (
                        (p.RoomId != null && p.RoomId == c.RoomId)
                        || (p.RoomId == null && c.RoomId == null && p.DistrictId == c.DistrictId)
                    )
                )
            );

        creatureQuery = CreatureLocationFiltering.ApplyFilters(
            creatureQuery,
            query.ExcludingCreatureId,
            query.CreatureTypes,
            query.IncludeDead
        );

        return await CreatureLocationFiltering.BuildSummaries(
            context,
            creatureQuery,
            cancellationToken
        );
    }
}
