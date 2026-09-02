using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Quests.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Queries;

public class GetWorldMapQuery
{
    public required Guid PlayerId { get; init; }
}

public record WorldMapRoad(Guid Id, string Name, Guid OriginStateId, Guid DestinationStateId);

public record WorldMapCorpse(Guid Id, string Name, Guid StateId, int ItemCount);

public record WorldMapQuestMarker(Guid QuestId, string ObjectiveName, Guid StateId);

public record WorldMapResult(
    IReadOnlyList<Country> Countries,
    IReadOnlyList<State> States,
    IReadOnlyList<City> Cities,
    IReadOnlyList<WorldMapRoad> Roads,
    Guid PlayerStateId,
    IReadOnlyList<WorldMapCorpse> Corpses,
    IReadOnlyList<WorldMapQuestMarker> QuestMarkers
);

internal class GetWorldMapQueryHandler(
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetCountriesByWorldIdQuery, IReadOnlyCollection<Country>> getCountriesByWorldId,
    IQueryHandler<GetStatesByWorldIdQuery, IReadOnlyCollection<State>> getStatesByWorldId,
    IQueryHandler<GetCitiesByWorldIdQuery, IReadOnlyCollection<City>> getCitiesByWorldId,
    IQueryHandler<GetCorpsesByOwnerQuery, IReadOnlyCollection<Creature>> getCorpsesByOwner,
    IQueryHandler<GetItemCountsByOwnersQuery, IReadOnlyDictionary<Guid, int>> getItemCountsByOwners,
    IQueryHandler<
        GetInProgressLocationObjectivesQuery,
        IReadOnlyCollection<InProgressLocationObjective>
    > getInProgressLocationObjectives,
    IQueryHandler<GetLocationsByIdsQuery, IReadOnlyDictionary<Guid, Location>> getLocationsByIds,
    IQueryHandler<
        GetCrossStateConnectorsQuery,
        IReadOnlyList<CrossStateConnector>
    > getCrossStateConnectors
) : IQueryHandler<GetWorldMapQuery, WorldMapResult>
{
    public async Task<WorldMapResult> Handle(
        GetWorldMapQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var player =
            await getCreatureById.Handle(
                new GetCreatureByIdQuery { Id = query.PlayerId },
                cancellationToken
            ) ?? throw new EntityNotFoundException(nameof(Creature), query.PlayerId);

        var worldId = player.WorldId;

        var countryResults = await getCountriesByWorldId.Handle(
            new GetCountriesByWorldIdQuery { WorldId = worldId },
            cancellationToken
        );
        var countries = countryResults.ToArray();

        var stateResults = await getStatesByWorldId.Handle(
            new GetStatesByWorldIdQuery { WorldId = worldId },
            cancellationToken
        );
        var states = stateResults.ToArray();

        var cityResults = await getCitiesByWorldId.Handle(
            new GetCitiesByWorldIdQuery { WorldId = worldId },
            cancellationToken
        );
        var cities = cityResults.ToArray();

        var roads = await ResolveRoads(worldId, cancellationToken);
        var playerStateId = await ResolvePlayerStateId(player.LocationId, cancellationToken);
        var corpses = await ResolveCorpses(worldId, query.PlayerId, cancellationToken);
        var questMarkers = await ResolveQuestMarkers(worldId, query.PlayerId, cancellationToken);

        return new WorldMapResult(
            countries,
            states,
            cities,
            roads,
            playerStateId,
            corpses,
            questMarkers
        );
    }

    private async Task<Guid> ResolvePlayerStateId(
        Guid locationId,
        CancellationToken cancellationToken
    )
    {
        var locationsById = await getLocationsByIds.Handle(
            new GetLocationsByIdsQuery { Ids = [locationId] },
            cancellationToken
        );
        return locationsById[locationId].StateId;
    }

    private async Task<IReadOnlyList<WorldMapRoad>> ResolveRoads(
        Guid worldId,
        CancellationToken cancellationToken
    )
    {
        var connectors = await getCrossStateConnectors.Handle(
            new GetCrossStateConnectorsQuery { WorldId = worldId },
            cancellationToken
        );

        // World generation links two states with a connector in each direction, so collapse each unordered state pair down to a single road for display.
        return connectors
            .Select(connector => new WorldMapRoad(
                connector.Id,
                connector.Name,
                connector.OriginStateId,
                connector.DestinationStateId
            ))
            .DistinctBy(road =>
                road.OriginStateId.CompareTo(road.DestinationStateId) <= 0
                    ? (road.OriginStateId, road.DestinationStateId)
                    : (road.DestinationStateId, road.OriginStateId)
            )
            .ToArray();
    }

    private async Task<IReadOnlyList<WorldMapCorpse>> ResolveCorpses(
        Guid worldId,
        Guid playerId,
        CancellationToken cancellationToken
    )
    {
        var corpseResults = await getCorpsesByOwner.Handle(
            new GetCorpsesByOwnerQuery { WorldId = worldId, OwnerId = playerId },
            cancellationToken
        );
        var corpses = corpseResults.ToArray();

        if (corpses.Length == 0)
        {
            return [];
        }

        var corpseIds = corpses.Select(corpse => corpse.Id).ToArray();
        var corpseLocationIds = corpses.Select(corpse => corpse.LocationId).Distinct().ToArray();

        var corpseLocationsById = await getLocationsByIds.Handle(
            new GetLocationsByIdsQuery { Ids = corpseLocationIds },
            cancellationToken
        );
        var stateIdByLocationId = corpseLocationsById.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.StateId
        );

        var itemCountsByOwner = await getItemCountsByOwners.Handle(
            new GetItemCountsByOwnersQuery { OwnerIds = corpseIds, OwnerType = OwnerType.Creature },
            cancellationToken
        );

        return corpses
            .Select(corpse => new WorldMapCorpse(
                corpse.Id,
                corpse.Name,
                stateIdByLocationId[corpse.LocationId],
                itemCountsByOwner.GetValueOrDefault(corpse.Id, 0)
            ))
            .Where(corpse => corpse.ItemCount > 0)
            .ToArray();
    }

    private async Task<IReadOnlyList<WorldMapQuestMarker>> ResolveQuestMarkers(
        Guid worldId,
        Guid playerId,
        CancellationToken cancellationToken
    )
    {
        var objectives = await getInProgressLocationObjectives.Handle(
            new GetInProgressLocationObjectivesQuery { PlayerId = playerId, WorldId = worldId },
            cancellationToken
        );

        if (objectives.Count == 0)
        {
            return [];
        }

        var locationIds = objectives.Select(objective => objective.LocationId).Distinct().ToArray();

        var locationsById = await getLocationsByIds.Handle(
            new GetLocationsByIdsQuery { Ids = locationIds },
            cancellationToken
        );

        return objectives
            .Select(objective => new WorldMapQuestMarker(
                objective.QuestId,
                objective.ObjectiveName,
                locationsById[objective.LocationId].StateId
            ))
            .ToArray();
    }
}
