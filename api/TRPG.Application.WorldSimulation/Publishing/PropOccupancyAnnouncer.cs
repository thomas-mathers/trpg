using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Props.Queries;
using TRPG.Application.Scenes;
using TRPG.Application.Scenes.Results;
using TRPG.Application.Worlds.Commands;
using TRPG.Application.WorldSimulation.Queries;

namespace TRPG.Application.WorldSimulation.Publishing;

internal class PropOccupancyAnnouncer(
    IQueryHandler<
        GetActiveLocationPlayersQuery,
        IReadOnlyCollection<ActiveLocationPlayer>
    > getActiveLocationPlayers,
    IQueryHandler<GetSeatOccupantsByIdsQuery, IReadOnlyDictionary<Guid, Guid?>> getSeatOccupants,
    ICommandHandler<StampWorldStateCommand, WorldStateStamp> stampWorldState,
    PublishedSceneRegistry publishedScenes,
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
            var previous = publishedScenes.Find(player.PlayerId);
            if (previous is null || previous.LocationId != player.LocationId)
            {
                continue;
            }

            await AnnounceTo(worldId, player, previous, cancellationToken);
        }
    }

    private async Task AnnounceTo(
        Guid worldId,
        ActiveLocationPlayer player,
        SceneResult previous,
        CancellationToken cancellationToken
    )
    {
        var occupants = await getSeatOccupants.Handle(
            new GetSeatOccupantsByIdsQuery
            {
                SeatIds = previous.NearbyProps.Select(prop => prop.Id).ToArray(),
            },
            cancellationToken
        );
        var changes = PropOccupancyProjection.Diff(previous, occupants);
        if (changes.Count == 0)
        {
            return;
        }

        var stamp = await stampWorldState.Handle(
            new StampWorldStateCommand { WorldId = worldId },
            cancellationToken
        );
        scenePublisher.PublishPropOccupancy(player.PlayerId, changes, stamp);
    }
}
