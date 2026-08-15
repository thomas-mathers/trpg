using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Handling;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Creatures.Queries;

public class GetNearbyCreaturesQuery
{
    public required Guid PlayerId { get; init; }
    public Guid? ExcludingCreatureId { get; init; }
    public IReadOnlyCollection<CreatureType>? CreatureTypes { get; init; }
    public bool IncludeDead { get; init; } = true;
}

internal class GetNearbyCreaturesQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetNearbyCreaturesQuery, IReadOnlyCollection<CreatureSummary>>
{
    public async Task<IReadOnlyCollection<CreatureSummary>> Handle(
        GetNearbyCreaturesQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var creatureQuery = context
            .Creatures.AsNoTracking()
            .Where(c =>
                context.Creatures.Any(p =>
                    p.Id == query.PlayerId && p.WorldId == c.WorldId && p.LocationId == c.LocationId
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
