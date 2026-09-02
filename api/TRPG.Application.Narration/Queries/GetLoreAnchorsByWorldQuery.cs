using Microsoft.Extensions.Caching.Memory;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Narration.Mappers;
using TRPG.Application.Narration.Results;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Narration.Queries;

public class GetLoreAnchorsByWorldQuery
{
    public required Guid WorldId { get; init; }
}

internal class GetLoreAnchorsByWorldQueryHandler(
    IMemoryCache cache,
    IQueryHandler<
        GetCreaturesInWorldQuery,
        IReadOnlyCollection<CreatureSummary>
    > getAllCreaturesInWorld,
    IQueryHandler<GetBuildingsByWorldIdQuery, IReadOnlyCollection<Building>> getBuildingsByWorldId,
    IQueryHandler<GetDistrictsByWorldIdQuery, IReadOnlyCollection<District>> getDistrictsByWorldId,
    IQueryHandler<GetWorldQuery, World?> getWorld,
    IQueryHandler<GetCountriesByWorldIdQuery, IReadOnlyCollection<Country>> getCountriesByWorldId,
    IQueryHandler<GetStatesByWorldIdQuery, IReadOnlyCollection<State>> getStatesByWorldId,
    IQueryHandler<GetCitiesByWorldIdQuery, IReadOnlyCollection<City>> getCitiesByWorldId
) : IQueryHandler<GetLoreAnchorsByWorldQuery, IReadOnlyCollection<LoreAnchorResult>>
{
    public static string CacheKey(Guid worldId) => $"namedEntities:{worldId}";

    public async Task<IReadOnlyCollection<LoreAnchorResult>> Handle(
        GetLoreAnchorsByWorldQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var entities = await cache.GetOrCreateAsync(
            CacheKey(query.WorldId),
            async _ => await BuildEntities(query.WorldId, cancellationToken)
        );
        return entities ?? [];
    }

    private async Task<LoreAnchorResult[]> BuildEntities(
        Guid worldId,
        CancellationToken cancellationToken
    )
    {
        var creatureSummaries = await getAllCreaturesInWorld.Handle(
            new GetCreaturesInWorldQuery { WorldId = worldId },
            cancellationToken
        );
        var creatures = creatureSummaries
            .Select(c => new LoreAnchorResult(
                c.Id,
                c.Name,
                LoreAnchorType.Creature,
                c.CreatureType.ToString(),
                c.Biography
            ))
            .ToArray();

        var buildingRows = await getBuildingsByWorldId.Handle(
            new GetBuildingsByWorldIdQuery { WorldId = worldId },
            cancellationToken
        );
        var buildings = buildingRows
            .Select(b => new LoreAnchorResult(
                b.Id,
                b.Name,
                LoreAnchorType.Building,
                b.BuildingType.ToDisplayName(),
                b.Description
            ))
            .ToArray();

        var districtRows = await getDistrictsByWorldId.Handle(
            new GetDistrictsByWorldIdQuery { WorldId = worldId },
            cancellationToken
        );
        var districts = districtRows
            .Select(d => new LoreAnchorResult(
                d.Id,
                d.Name,
                LoreAnchorType.District,
                d.DistrictType.ToDisplayName(),
                d.Description
            ))
            .ToArray();

        var worldEntity = await getWorld.Handle(
            new GetWorldQuery { WorldId = worldId },
            cancellationToken
        );
        var world =
            worldEntity != null
                ? new[]
                {
                    new LoreAnchorResult(
                        worldEntity.Id,
                        worldEntity.Name,
                        LoreAnchorType.World,
                        null,
                        worldEntity.Description
                    ),
                }
                : [];

        var countryRows = await getCountriesByWorldId.Handle(
            new GetCountriesByWorldIdQuery { WorldId = worldId },
            cancellationToken
        );
        var countries = countryRows
            .Select(c => new LoreAnchorResult(
                c.Id,
                c.Name,
                LoreAnchorType.Country,
                c.Focus.ToString(),
                c.Description
            ))
            .ToArray();

        var stateRows = await getStatesByWorldId.Handle(
            new GetStatesByWorldIdQuery { WorldId = worldId },
            cancellationToken
        );
        var states = stateRows
            .Select(s => new LoreAnchorResult(
                s.Id,
                s.Name,
                LoreAnchorType.State,
                null,
                s.Description
            ))
            .ToArray();

        var cityRows = await getCitiesByWorldId.Handle(
            new GetCitiesByWorldIdQuery { WorldId = worldId },
            cancellationToken
        );
        var cities = cityRows
            .Select(c => new LoreAnchorResult(
                c.Id,
                c.Name,
                LoreAnchorType.City,
                null,
                c.Description
            ))
            .ToArray();

        return creatures
            .Concat(buildings)
            .Concat(districts)
            .Concat(world)
            .Concat(countries)
            .Concat(states)
            .Concat(cities)
            .ToArray();
    }
}
