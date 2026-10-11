using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Props.Queries;
using TRPG.Application.Worlds.Commands;
using TRPG.Application.WorldSimulation.Events;
using TRPG.Application.WorldSimulation.Queries;
using TRPG.Application.WorldSimulation.Subscriptions;

namespace TRPG.Application.WorldSimulation.Publishing;

internal class PropOccupancyAnnouncer(
    IQueryHandler<
        GetActiveLocationPlayersQuery,
        IReadOnlyCollection<ActiveLocationPlayer>
    > getActiveLocationPlayers,
    IQueryHandler<GetSeatOccupantsByIdsQuery, IReadOnlyDictionary<Guid, Guid?>> getSeatOccupants,
    ICommandHandler<StampWorldStateCommand, WorldStateStamp> stampWorldState,
    SceneSubscriptions subscriptions,
    ScenePublisher scenePublisher
)
{
    public async Task Announce(
        Guid worldId,
        IReadOnlyCollection<Guid> locationIds,
        CancellationToken cancellationToken
    )
    {
        var players = await getActiveLocationPlayers.Handle(
            new GetActiveLocationPlayersQuery { WorldId = worldId },
            cancellationToken
        );

        foreach (var player in players.Where(player => locationIds.Contains(player.LocationId)))
        {
            var watch = subscriptions.SnapshotOf(worldId, player.PlayerId);
            if (watch is null || watch.LocationId != player.LocationId)
            {
                continue;
            }

            await AnnounceTo(worldId, player, watch, cancellationToken);
        }
    }

    private async Task AnnounceTo(
        Guid worldId,
        ActiveLocationPlayer player,
        WatchSnapshot watch,
        CancellationToken cancellationToken
    )
    {
        var occupants = await getSeatOccupants.Handle(
            new GetSeatOccupantsByIdsQuery { SeatIds = [.. watch.Seats.Keys] },
            cancellationToken
        );
        var changes = occupants
            .Where(seat => watch.Seats.ContainsKey(seat.Key))
            .Select(seat => new PropOccupancy(
                seat.Key,
                seat.Value is not null,
                seat.Value == player.PlayerId
            ))
            .Where(occupancy => occupancy != watch.Seats[occupancy.PropId])
            .ToArray();
        if (changes.Length == 0)
        {
            return;
        }

        var stamp = await stampWorldState.Handle(
            new StampWorldStateCommand { WorldId = worldId },
            cancellationToken
        );
        scenePublisher.PublishPropOccupancy(worldId, player.LocationId, stamp, changes);
        subscriptions.RecordSeats(worldId, player.PlayerId, changes);
    }
}
