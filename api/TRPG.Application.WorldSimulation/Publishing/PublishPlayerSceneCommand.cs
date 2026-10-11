using TRPG.Application.Common.Clocks;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.Scenes.Results;
using TRPG.Application.Worlds.Commands;
using TRPG.Application.WorldSimulation.Subscriptions;

namespace TRPG.Application.WorldSimulation.Publishing;

public class PublishPlayerSceneCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
}

internal class PublishPlayerSceneCommandHandler(
    ScenePublisher scenePublisher,
    SceneSubscriptions subscriptions,
    IWorldClock worldClock,
    ICommandHandler<StampWorldStateCommand, WorldStateStamp> stampWorldState,
    IQueryHandler<GetCurrentSceneQuery, SceneResult> getCurrentScene
) : ICommandHandler<PublishPlayerSceneCommand>
{
    public async Task Handle(
        PublishPlayerSceneCommand command,
        CancellationToken cancellationToken = default
    )
    {
        // Stamped before the scene is read so a later-numbered snapshot never describes older state.
        var stamp = await stampWorldState.Handle(
            new StampWorldStateCommand { WorldId = command.WorldId },
            cancellationToken
        );
        var gameTime = await worldClock.GetCurrent(command.WorldId, cancellationToken);
        var scene = await getCurrentScene.Handle(
            new GetCurrentSceneQuery
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                GameTime = gameTime,
            },
            cancellationToken
        );

        scenePublisher.Publish(command.WorldId, scene, stamp);
        subscriptions.Watch(command.WorldId, command.PlayerId, scene);
    }
}
