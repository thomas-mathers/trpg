using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Mappers;
using TRPG.Application.Creatures.Results;
using TRPG.Data;
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
        TrpgDbContext context,
        IQueryable<Creature> creatureQuery,
        CancellationToken cancellationToken
    )
    {
        var rows = await creatureQuery
            .Select(p => new
            {
                Creature = p,
                Gold = context
                    .Items.OfType<Gold>()
                    .Where(g => g.Ownership.OwnerId == p.Id)
                    .Select(g => (int?)g.Quantity)
                    .FirstOrDefault(),
                Location = context
                    .Locations.Where(l => p.LocationId == l.Id)
                    .Select(l => new
                    {
                        l.StateId,
                        l.CityId,
                        l.DistrictId,
                        l.RoomId,
                    })
                    .FirstOrDefault(),
            })
            .ToArrayAsync(cancellationToken);

        return rows.Select(r =>
                r.Creature.ToResult(
                    r.Gold ?? 0,
                    r.Location?.StateId
                        ?? throw new InvalidOperationException("Creature location was not found."),
                    r.Location?.CityId,
                    r.Location?.DistrictId,
                    r.Location?.RoomId
                )
            )
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

internal class GetCreaturesAtLocationQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetCreaturesAtLocationQuery, IReadOnlyCollection<CreatureResult>>
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
            context,
            creatureQuery,
            cancellationToken
        );
    }
}
