using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Scenes;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.Scenes.Results;
using TRPG.Application.Worlds.Commands;
using TRPG.Application.WorldSimulation.Queries;
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
    PublishedSceneRegistry publishedScenes,
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
        var previous = publishedScenes.Find(player.PlayerId);
        if (previous is null || previous.LocationId != player.LocationId)
        {
            return;
        }

        var refreshed = await Refresh(worldId, now, player, creatureIds, cancellationToken);
        var arrivedIds = refreshed
            .Where(creature => creature.Id != player.PlayerId)
            .Select(creature => creature.Id)
            .Except(previous.NearbyCreatures.Select(creature => creature.Id))
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

        var refreshedNearby = refreshed.Where(creature => creature.Id != player.PlayerId).ToArray();
        var current = previous with
        {
            Player =
                refreshed.SingleOrDefault(creature => creature.Id == player.PlayerId)
                ?? previous.Player,
            NearbyCreatures =
            [
                .. previous.NearbyCreatures.Where(creature => !creatureIds.Contains(creature.Id)),
                .. refreshedNearby,
            ],
        };
        MovementSnapshotLog.Write(logger, player.LocationId, now, refreshedNearby);
        if (!SceneSemanticComparer.HasPlayerVisibleChange(previous, current))
        {
            return;
        }

        var stamp = await stampWorldState.Handle(
            new StampWorldStateCommand { WorldId = worldId },
            cancellationToken
        );
        scenePublisher.PublishCreatureChanges(player.PlayerId, current, stamp);
    }

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
