using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Scenes;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.Scenes.Results;
using TRPG.Application.Worlds.Commands;
using TRPG.Application.WorldSimulation.Queries;
using TRPG.Application.WorldSimulation.Subscriptions;
using TRPG.Domain;

namespace TRPG.Application.WorldSimulation.Publishing;

internal class CreatureChangeAnnouncer(
    IQueryHandler<
        GetActiveLocationPlayersQuery,
        IReadOnlyCollection<ActiveLocationPlayer>
    > getActiveLocationPlayers,
    IQueryHandler<GetSceneCreaturesQuery, IReadOnlyCollection<SceneCreatureInfo>> getSceneCreatures,
    ICommandHandler<StampWorldStateCommand, WorldStateStamp> stampWorldState,
    ArrivalCatchUp arrivalCatchUp,
    SceneSubscriptions subscriptions,
    ScenePublisher scenePublisher,
    ILogger<CreatureChangeAnnouncer> logger
)
{
    public async Task Announce(
        Guid worldId,
        GameInstant now,
        IReadOnlyDictionary<Guid, IReadOnlySet<Guid>> changedCreatureIdsByLocationId,
        CancellationToken cancellationToken
    )
    {
        var players = await getActiveLocationPlayers.Handle(
            new GetActiveLocationPlayersQuery { WorldId = worldId },
            cancellationToken
        );

        foreach (var player in players)
        {
            if (changedCreatureIdsByLocationId.TryGetValue(player.LocationId, out var creatureIds))
            {
                await AnnounceTo(worldId, now, player, creatureIds, cancellationToken);
            }
        }
    }

    private async Task AnnounceTo(
        Guid worldId,
        GameInstant now,
        ActiveLocationPlayer player,
        IReadOnlySet<Guid> creatureIds,
        CancellationToken cancellationToken
    )
    {
        var watch = subscriptions.SnapshotOf(worldId, player.PlayerId);
        if (watch is null || watch.LocationId != player.LocationId)
        {
            return;
        }

        var refreshed = await Refresh(worldId, now, player, creatureIds, cancellationToken);
        var arrivedIds = NearbyOf(refreshed, player)
            .Select(creature => creature.Id)
            .Except(watch.KnownCreatureIds)
            .ToArray();
        if (arrivedIds.Length > 0)
        {
            await arrivalCatchUp.Run(
                worldId,
                player.LocationId,
                arrivedIds,
                now,
                cancellationToken
            );
            refreshed = await Refresh(worldId, now, player, creatureIds, cancellationToken);
        }

        var nearby = NearbyOf(refreshed, player);
        MovementSnapshotLog.Write(logger, player.LocationId, now, nearby);
        var arrived = nearby
            .Where(creature => !watch.KnownCreatureIds.Contains(creature.Id))
            .ToArray();
        var leftIds = creatureIds
            .Where(id => watch.KnownCreatureIds.Contains(id))
            .Except(nearby.Select(creature => creature.Id))
            .ToHashSet();
        var updated = refreshed
            .Where(creature =>
                creature.Id == player.PlayerId || watch.KnownCreatureIds.Contains(creature.Id)
            )
            .ToArray();
        if (arrived.Length == 0 && leftIds.Count == 0 && updated.Length == 0)
        {
            return;
        }

        var stamp = await stampWorldState.Handle(
            new StampWorldStateCommand { WorldId = worldId },
            cancellationToken
        );
        scenePublisher.PublishCreatureChanges(
            worldId,
            player.LocationId,
            stamp,
            leftIds,
            arrived,
            updated
        );
        subscriptions.RecordCreatureMembership(
            worldId,
            player.PlayerId,
            arrived.Select(creature => creature.Id).ToArray(),
            leftIds
        );
    }

    private static SceneCreatureInfo[] NearbyOf(
        IReadOnlyCollection<SceneCreatureInfo> creatures,
        ActiveLocationPlayer player
    ) => [.. creatures.Where(creature => creature.Id != player.PlayerId)];

    private Task<IReadOnlyCollection<SceneCreatureInfo>> Refresh(
        Guid worldId,
        GameInstant now,
        ActiveLocationPlayer player,
        IReadOnlySet<Guid> creatureIds,
        CancellationToken cancellationToken
    ) =>
        getSceneCreatures.Handle(
            new GetSceneCreaturesQuery
            {
                WorldId = worldId,
                PlayerId = player.PlayerId,
                GameTime = now,
                CreatureIds = creatureIds,
            },
            cancellationToken
        );
}
