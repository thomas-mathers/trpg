using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Scenes;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.Scenes.Results;
using TRPG.Application.Worlds.Commands;
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
    PublishedSceneRegistry publishedScenes,
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
        var previous = publishedScenes.Find(playerId);
        if (previous is null || SceneSemanticComparer.HasStaticChange(previous, scene))
        {
            scenePublisher.Publish(worldId, playerId, scene, stamp);
            return new AnnouncedScene(scene.LocationId, PublishedSnapshot: true);
        }

        await AnnounceCreatureChanges(worldId, playerId, previous, now, cancellationToken);
        return new AnnouncedScene(scene.LocationId, PublishedSnapshot: false);
    }

    private Task AnnounceCreatureChanges(
        Guid worldId,
        Guid playerId,
        SceneResult published,
        GameInstant now,
        CancellationToken cancellationToken
    )
    {
        var creatureIds = published
            .NearbyCreatures.Select(creature => creature.Id)
            .Append(playerId)
            .ToHashSet();
        return creatureChangeAnnouncer.Announce(
            worldId,
            now,
            new Dictionary<Guid, IReadOnlySet<Guid>> { [published.LocationId] = creatureIds },
            cancellationToken
        );
    }

    private sealed record AnnouncedScene(Guid LocationId, bool PublishedSnapshot);
}
