using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Inventory.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Queries;

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
    TrpgDbContext context,
    IQueryHandler<GetItemCountsByOwnersQuery, IReadOnlyDictionary<Guid, int>> getItemCountsByOwners
) : IQueryHandler<GetWorldMapQuery, WorldMapResult>
{
    public async Task<WorldMapResult> Handle(
        GetWorldMapQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var player =
            await context
                .Creatures.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == query.PlayerId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Creature), query.PlayerId);

        var worldId = player.WorldId;

        var countries = await context
            .Countries.AsNoTracking()
            .Where(country => country.WorldId == worldId)
            .ToArrayAsync(cancellationToken);

        var states = await context
            .States.AsNoTracking()
            .Where(state => state.WorldId == worldId)
            .ToArrayAsync(cancellationToken);

        var cities = await context
            .Cities.AsNoTracking()
            .Where(city => city.WorldId == worldId)
            .ToArrayAsync(cancellationToken);

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
    ) =>
        await context
            .Locations.AsNoTracking()
            .Where(location => location.Id == locationId)
            .Select(location => location.StateId)
            .FirstAsync(cancellationToken);

    private async Task<IReadOnlyList<WorldMapRoad>> ResolveRoads(
        Guid worldId,
        CancellationToken cancellationToken
    )
    {
        var roads = await (
            from connector in context.LocationConnectors.AsNoTracking()
            where connector.WorldId == worldId
            join origin in context.Locations.AsNoTracking()
                on connector.OriginLocationId equals origin.Id
            join destination in context.Locations.AsNoTracking()
                on connector.DestinationLocationId equals destination.Id
            where origin.StateId != destination.StateId
            select new WorldMapRoad(
                connector.Id,
                connector.Name,
                origin.StateId,
                destination.StateId
            )
        ).ToArrayAsync(cancellationToken);

        // World generation links two states with a connector in each direction, so collapse each unordered state pair down to a single road for display.
        return roads
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
        var corpses = await context
            .Creatures.AsNoTracking()
            .Where(creature =>
                creature.WorldId == worldId
                && creature.PlayerCorpseOwnerId == playerId
                && creature.State == CreatureState.Dead
            )
            .ToArrayAsync(cancellationToken);

        if (corpses.Length == 0)
        {
            return [];
        }

        var corpseIds = corpses.Select(corpse => corpse.Id).ToArray();
        var corpseLocationIds = corpses.Select(corpse => corpse.LocationId).Distinct().ToArray();

        var stateIdByLocationId = await context
            .Locations.AsNoTracking()
            .Where(location => corpseLocationIds.AsEnumerable().Contains(location.Id))
            .ToDictionaryAsync(
                location => location.Id,
                location => location.StateId,
                cancellationToken
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
        var objectives = await context
            .CreatureQuestObjectives.AsNoTracking()
            .Include(objective => objective.Objective)
            .Where(objective => objective.CreatureId == playerId && objective.WorldId == worldId)
            .Where(objective => objective.Amount < objective.Objective.RequiredAmount)
            .Where(objective => objective.Objective.LocationId != null)
            .ToArrayAsync(cancellationToken);

        if (objectives.Length == 0)
        {
            return [];
        }

        var locationIds = objectives
            .Select(objective => objective.Objective.LocationId!.Value)
            .Distinct()
            .ToArray();

        var stateIdByLocationId = await context
            .Locations.AsNoTracking()
            .Where(location => locationIds.AsEnumerable().Contains(location.Id))
            .ToDictionaryAsync(
                location => location.Id,
                location => location.StateId,
                cancellationToken
            );

        return objectives
            .Select(objective => new WorldMapQuestMarker(
                objective.Objective.QuestId,
                objective.Objective.Name,
                stateIdByLocationId[objective.Objective.LocationId!.Value]
            ))
            .ToArray();
    }
}
