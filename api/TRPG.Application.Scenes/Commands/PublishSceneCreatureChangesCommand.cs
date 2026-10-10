using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.Scenes.Results;
using TRPG.Application.Worlds.Commands;
using TRPG.Domain;
using TRPG.Domain.Models;

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
    ScenePublisher scenePublisher,
    ILogger<PublishSceneCreatureChangesCommandHandler> logger
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
        LogMovementSnapshots(command, refreshed);
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

    private void LogMovementSnapshots(
        PublishSceneCreatureChangesCommand command,
        IEnumerable<SceneCreatureInfo> creatures
    )
    {
        foreach (var creature in creatures)
        {
            var path = creature.Walk?.Path ?? [];
            var pathMeters = PathLength(path);
            var logLevel =
                creature.Movement == CreatureMovement.Walking && pathMeters <= 1e-9
                    ? LogLevel.Warning
                    : LogLevel.Debug;
            logger.Log(
                logLevel,
                "Scene movement snapshot for {CreatureName} ({CreatureId}) in {LocationId} at {GameTime}: movement {Movement}, posture {Posture}, placement ({X:F2}, {Y:F2}), walk started {WalkStartedAt}, points {PathPointCount}, remaining meters {PathMeters:F2}, pace {MetersPerGameSecond:F3}, leaves {LeavesAtEnd}",
                creature.Name,
                creature.Id,
                command.LocationId,
                command.GameTime,
                creature.Movement,
                creature.Posture,
                creature.Placement.X,
                creature.Placement.Y,
                creature.Walk?.StartedAt,
                path.Count,
                pathMeters,
                creature.Walk?.MetersPerGameSecond,
                creature.Walk?.LeavesAtEnd
            );
        }
    }

    private static double PathLength(IReadOnlyList<Point> points) =>
        points
            .Zip(points.Skip(1))
            .Sum(pair =>
                Math.Sqrt(
                    Math.Pow(pair.Second.X - pair.First.X, 2)
                        + Math.Pow(pair.Second.Y - pair.First.Y, 2)
                )
            );

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
