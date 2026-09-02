using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Results;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Queries;

public class GetNearbyCreaturesQuery
{
    public required Guid PlayerId { get; init; }
    public Guid? ExcludingCreatureId { get; init; }
    public IReadOnlyCollection<CreatureType>? CreatureTypes { get; init; }
    public bool IncludeDead { get; init; } = true;
}

internal class GetNearbyCreaturesQueryHandler(
    ICreaturesDbContext context,
    IQueryHandler<
        GetGoldQuantitiesByOwnersQuery,
        IReadOnlyDictionary<Guid, int>
    > getGoldQuantitiesByOwners,
    IQueryHandler<GetLocationsByIdsQuery, IReadOnlyDictionary<Guid, Location>> getLocationsByIds
) : IQueryHandler<GetNearbyCreaturesQuery, IReadOnlyCollection<CreatureResult>>
{
    public async Task<IReadOnlyCollection<CreatureResult>> Handle(
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
            query.ExcludingCreatureId is { } excludingCreatureId ? [excludingCreatureId] : [],
            query.CreatureTypes,
            query.IncludeDead
        );

        return await CreatureLocationFiltering.BuildSummaries(
            getGoldQuantitiesByOwners,
            getLocationsByIds,
            creatureQuery,
            cancellationToken
        );
    }
}
