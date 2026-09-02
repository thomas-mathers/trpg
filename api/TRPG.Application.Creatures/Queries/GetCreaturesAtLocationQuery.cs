using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Mappers;
using TRPG.Application.Creatures.Results;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Queries;

internal static class CreatureLocationFiltering
{
    public static IQueryable<Creature> ApplyFilters(
        IQueryable<Creature> query,
        IReadOnlyCollection<Guid> excludedCreatureIds,
        IReadOnlyCollection<CreatureType>? creatureTypes,
        bool includeDead
    )
    {
        if (excludedCreatureIds.Count > 0)
        {
            query = query.Where(p => !excludedCreatureIds.AsEnumerable().Contains(p.Id));
        }

        if (creatureTypes is not null)
        {
            query = query.Where(p => creatureTypes.Contains(p.CreatureType));
        }

        if (!includeDead)
        {
            query = query.Where(p => p.State != CreatureState.Dead);
        }

        return query;
    }

    public static async Task<IReadOnlyCollection<CreatureResult>> BuildSummaries(
        IQueryHandler<
            GetGoldQuantitiesByOwnersQuery,
            IReadOnlyDictionary<Guid, int>
        > getGoldQuantitiesByOwners,
        IQueryHandler<
            GetLocationsByIdsQuery,
            IReadOnlyDictionary<Guid, Location>
        > getLocationsByIds,
        IQueryable<Creature> creatureQuery,
        CancellationToken cancellationToken
    )
    {
        var creatures = await creatureQuery.ToArrayAsync(cancellationToken);
        if (creatures.Length == 0)
        {
            return [];
        }

        var creatureIds = creatures.Select(creature => creature.Id).ToArray();
        var goldByCreatureId = await getGoldQuantitiesByOwners.Handle(
            new GetGoldQuantitiesByOwnersQuery
            {
                OwnerIds = creatureIds,
                OwnerType = OwnerType.Creature,
            },
            cancellationToken
        );

        var locationIds = creatures.Select(creature => creature.LocationId).Distinct().ToArray();
        var locationsById = await getLocationsByIds.Handle(
            new GetLocationsByIdsQuery { Ids = locationIds },
            cancellationToken
        );

        return creatures
            .Select(creature =>
            {
                var location =
                    locationsById.GetValueOrDefault(creature.LocationId)
                    ?? throw new InvalidOperationException("Creature location was not found.");
                return creature.ToResult(
                    goldByCreatureId.GetValueOrDefault(creature.Id, 0),
                    location.StateId,
                    location.CityId,
                    location.DistrictId,
                    location.RoomId
                );
            })
            .ToArray();
    }
}

public class GetCreaturesAtLocationQuery
{
    public required Guid WorldId { get; init; }
    public required Guid LocationId { get; init; }
    public Guid? ExcludingCreatureId { get; init; }
    public IReadOnlyCollection<Guid> ExcludedCreatureIds { get; init; } = [];
    public IReadOnlyCollection<CreatureType>? CreatureTypes { get; init; }
    public bool IncludeDead { get; init; } = true;
}

internal class GetCreaturesAtLocationQueryHandler(
    ICreaturesDbContext context,
    IQueryHandler<
        GetGoldQuantitiesByOwnersQuery,
        IReadOnlyDictionary<Guid, int>
    > getGoldQuantitiesByOwners,
    IQueryHandler<GetLocationsByIdsQuery, IReadOnlyDictionary<Guid, Location>> getLocationsByIds
) : IQueryHandler<GetCreaturesAtLocationQuery, IReadOnlyCollection<CreatureResult>>
{
    public async Task<IReadOnlyCollection<CreatureResult>> Handle(
        GetCreaturesAtLocationQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var creatureQuery = context
            .Creatures.AsNoTracking()
            .Where(p => p.WorldId == query.WorldId && p.LocationId == query.LocationId);

        creatureQuery = CreatureLocationFiltering.ApplyFilters(
            creatureQuery,
            query.ExcludingCreatureId is { } excludingCreatureId
                ? [excludingCreatureId, .. query.ExcludedCreatureIds]
                : query.ExcludedCreatureIds,
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
