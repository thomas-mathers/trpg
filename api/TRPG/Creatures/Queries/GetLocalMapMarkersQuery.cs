using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Props.Queries;
using TRPG.Domain.Models;

namespace TRPG.Creatures.Queries;

public class GetLocalMapMarkersQuery
{
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<Guid> ExploredLocationIds { get; init; }
    public required IReadOnlyCollection<Guid> RoomLocationIds { get; init; }
}

public enum LocalMapMarkerKind
{
    Chest,
    Lever,
    PlayerCorpse,
}

public enum LocalMapMarkerState
{
    ContainsItems,
    Empty,
    Unactivated,
    Activated,
    RecoverableLoot,
}

public record LocalMapMarker(
    Guid Id,
    Guid LocationId,
    string Name,
    LocalMapMarkerKind Kind,
    LocalMapMarkerState State,
    bool IsLocked,
    int? ItemCount = null
);

internal class GetLocalMapMarkersQueryHandler(
    IQueryHandler<GetInteractablePropsByLocationIdsQuery, IReadOnlyList<InteractableProp>> getProps,
    IQueryHandler<GetCorpsesByOwnerQuery, IReadOnlyCollection<Creature>> getCorpses,
    IQueryHandler<GetItemCountsByOwnersQuery, IReadOnlyDictionary<Guid, int>> getItemCounts
) : IQueryHandler<GetLocalMapMarkersQuery, IReadOnlyList<LocalMapMarker>>
{
    public async Task<IReadOnlyList<LocalMapMarker>> Handle(
        GetLocalMapMarkersQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var props = await getProps.Handle(
            new GetInteractablePropsByLocationIdsQuery { LocationIds = query.ExploredLocationIds },
            cancellationToken
        );

        var counts = await getItemCounts.Handle(
            new GetItemCountsByOwnersQuery
            {
                OwnerIds = props
                    .Where(prop => prop.IsPulled == null)
                    .Select(prop => prop.Id)
                    .ToArray(),
                OwnerType = OwnerType.Container,
            },
            cancellationToken
        );

        var corpses = await CorpseMarkers(query, cancellationToken);
        return props
            .Select(prop => PropMarker(prop, counts.GetValueOrDefault(prop.Id)))
            .Concat(corpses)
            .ToArray();
    }

    private static LocalMapMarker PropMarker(InteractableProp prop, int itemCount) =>
        new(
            Id: prop.Id,
            LocationId: prop.LocationId,
            Name: prop.Name,
            Kind: prop.IsPulled.HasValue ? LocalMapMarkerKind.Lever : LocalMapMarkerKind.Chest,
            State: prop.IsPulled switch
            {
                true => LocalMapMarkerState.Activated,
                false => LocalMapMarkerState.Unactivated,
                null => itemCount > 0
                    ? LocalMapMarkerState.ContainsItems
                    : LocalMapMarkerState.Empty,
            },
            IsLocked: prop.IsLocked
        );

    private async Task<IReadOnlyList<LocalMapMarker>> CorpseMarkers(
        GetLocalMapMarkersQuery query,
        CancellationToken cancellationToken
    )
    {
        var corpses = await getCorpses.Handle(
            new GetCorpsesByOwnerQuery { WorldId = query.WorldId, OwnerId = query.PlayerId },
            cancellationToken
        );
        var localCorpses = corpses
            .Where(corpse => query.RoomLocationIds.Contains(corpse.LocationId))
            .ToArray();

        var counts = await getItemCounts.Handle(
            new GetItemCountsByOwnersQuery
            {
                OwnerIds = localCorpses.Select(corpse => corpse.Id).ToArray(),
                OwnerType = OwnerType.Creature,
            },
            cancellationToken
        );
        return localCorpses
            .Where(corpse => counts.GetValueOrDefault(corpse.Id) > 0)
            .Select(corpse => new LocalMapMarker(
                Id: corpse.Id,
                LocationId: corpse.LocationId,
                Name: "Your corpse",
                Kind: LocalMapMarkerKind.PlayerCorpse,
                State: LocalMapMarkerState.RecoverableLoot,
                IsLocked: false,
                ItemCount: counts[corpse.Id]
            ))
            .ToArray();
    }
}
