using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.Scenes.Results;
using TRPG.Application.Worlds.Commands;
using TRPG.Application.WorldSimulation.Subscriptions;
using TRPG.Domain;

namespace TRPG.Application.WorldSimulation.Publishing;

public interface IActionSceneAnnouncer
{
    Task AnnounceActionChanges(
        Guid worldId,
        Guid playerId,
        GameInstant now,
        CancellationToken cancellationToken
    );

    Task AnnounceTimeAdvance(
        Guid worldId,
        Guid playerId,
        GameInstant now,
        CancellationToken cancellationToken
    );
}

internal sealed class ActionSceneAnnouncer(
    IQueryHandler<GetCurrentSceneQuery, SceneResult> getCurrentScene,
    ICommandHandler<StampWorldStateCommand, WorldStateStamp> stampWorldState,
    CreatureChangeAnnouncer creatureChangeAnnouncer,
    SceneSubscriptions subscriptions,
    ScenePublisher scenePublisher
) : IActionSceneAnnouncer
{
    public async Task AnnounceActionChanges(
        Guid worldId,
        Guid playerId,
        GameInstant now,
        CancellationToken cancellationToken
    ) => await Announce(worldId, playerId, now, cancellationToken);

    public async Task AnnounceTimeAdvance(
        Guid worldId,
        Guid playerId,
        GameInstant now,
        CancellationToken cancellationToken
    )
    {
        var published = await Announce(worldId, playerId, now, cancellationToken);
        if (!published.PublishedSnapshot)
        {
            var stamp = await stampWorldState.Handle(
                new StampWorldStateCommand { WorldId = worldId },
                cancellationToken
            );
            scenePublisher.PublishClockReanchored(worldId, published.LocationId, stamp);
        }
    }

    private async Task<AnnouncedScene> Announce(
        Guid worldId,
        Guid playerId,
        GameInstant now,
        CancellationToken cancellationToken
    )
    {
        // Stamped before the scene is read so a later-numbered snapshot never describes older state.
        var stamp = await stampWorldState.Handle(
            new StampWorldStateCommand { WorldId = worldId },
            cancellationToken
        );
        var scene = await getCurrentScene.Handle(
            new GetCurrentSceneQuery
            {
                WorldId = worldId,
                PlayerId = playerId,
                GameTime = now,
            },
            cancellationToken
        );
        var watch = subscriptions.SnapshotOf(worldId, playerId);
        if (watch is null || watch.LocationId != scene.LocationId)
        {
            scenePublisher.Publish(worldId, scene, stamp);
            subscriptions.Watch(worldId, playerId, scene);
            return new AnnouncedScene(scene.LocationId, PublishedSnapshot: true);
        }

        await AnnounceCreatureChanges(worldId, playerId, watch, now, cancellationToken);
        return new AnnouncedScene(scene.LocationId, PublishedSnapshot: false);
    }

    private Task AnnounceCreatureChanges(
        Guid worldId,
        Guid playerId,
        WatchSnapshot watch,
        GameInstant now,
        CancellationToken cancellationToken
    )
    {
        var creatureIds = watch.KnownCreatureIds.Append(playerId).ToHashSet();
        return creatureChangeAnnouncer.Announce(
            worldId,
            now,
            new Dictionary<Guid, IReadOnlySet<Guid>> { [watch.LocationId] = creatureIds },
            cancellationToken
        );
    }

    private sealed record AnnouncedScene(Guid LocationId, bool PublishedSnapshot);
}
