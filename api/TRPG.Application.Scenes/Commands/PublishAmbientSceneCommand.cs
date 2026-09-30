using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.Scenes.Results;
using TRPG.Application.Worlds.Commands;
using TRPG.Domain;

namespace TRPG.Application.Scenes.Commands;

public class PublishAmbientSceneCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class PublishAmbientSceneCommandHandler(
    IQueryHandler<GetCurrentSceneQuery, SceneResult> getCurrentScene,
    ICommandHandler<StampWorldStateCommand, WorldStateStamp> stampWorldState,
    PublishedSceneRegistry publishedScenes,
    ScenePublisher scenePublisher,
    CreatureMovementPublisher movementPublisher
) : ICommandHandler<PublishAmbientSceneCommand>
{
    public async Task Handle(
        PublishAmbientSceneCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var scene = await getCurrentScene.Handle(
            new GetCurrentSceneQuery
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                GameTime = command.GameTime,
            },
            cancellationToken
        );

        var previous = publishedScenes.Find(command.PlayerId);
        if (previous != null && !SceneSemanticComparer.HasPlayerVisibleChange(previous, scene))
        {
            return;
        }

        // The caller holds the world lease, so nothing mutates between the read above and this stamp.
        var stamp = await stampWorldState.Handle(
            new StampWorldStateCommand { WorldId = command.WorldId },
            cancellationToken
        );
        if (!scenePublisher.PublishIfChanged(command.PlayerId, scene, stamp))
        {
            return;
        }

        if (previous != null)
        {
            await movementPublisher.Publish(command.PlayerId, previous, scene, cancellationToken);
        }
    }
}
