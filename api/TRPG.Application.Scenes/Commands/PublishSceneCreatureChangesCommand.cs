using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.Scenes.Results;
using TRPG.Application.Worlds.Commands;
using TRPG.Domain;

namespace TRPG.Application.Scenes.Commands;

public class PublishSceneCreatureChangesCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid LocationId { get; init; }
    public required GameInstant GameTime { get; init; }
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
}

internal class PublishSceneCreatureChangesCommandHandler(
    IQueryHandler<GetSceneCreaturesQuery, IReadOnlyCollection<SceneCreatureInfo>> getSceneCreatures,
    ICommandHandler<StampWorldStateCommand, WorldStateStamp> stampWorldState,
    ICommandHandler<PublishAmbientSceneCommand> publishAmbientScene,
    PublishedSceneRegistry publishedScenes,
    ScenePublisher scenePublisher
) : ICommandHandler<PublishSceneCreatureChangesCommand>
{
    public async Task Handle(
        PublishSceneCreatureChangesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var previous = publishedScenes.Find(command.PlayerId);
        if (previous == null || previous.LocationId != command.LocationId)
        {
            await PublishFullScene(command, cancellationToken);
            return;
        }

        var refreshed = await getSceneCreatures.Handle(
            new GetSceneCreaturesQuery
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                GameTime = command.GameTime,
                CreatureIds = command.CreatureIds,
            },
            cancellationToken
        );
        var current = previous with
        {
            NearbyCreatures =
            [
                .. previous.NearbyCreatures.Where(creature =>
                    !command.CreatureIds.Contains(creature.Id)
                ),
                .. refreshed,
            ],
        };
        if (!SceneSemanticComparer.HasPlayerVisibleChange(previous, current))
        {
            return;
        }

        // The caller holds the world lease, so nothing mutates between the read above and this stamp.
        var stamp = await stampWorldState.Handle(
            new StampWorldStateCommand { WorldId = command.WorldId },
            cancellationToken
        );
        scenePublisher.PublishIfChanged(command.WorldId, command.PlayerId, current, stamp);
    }

    private Task PublishFullScene(
        PublishSceneCreatureChangesCommand command,
        CancellationToken cancellationToken
    ) =>
        publishAmbientScene.Handle(
            new PublishAmbientSceneCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                GameTime = command.GameTime,
            },
            cancellationToken
        );
}
