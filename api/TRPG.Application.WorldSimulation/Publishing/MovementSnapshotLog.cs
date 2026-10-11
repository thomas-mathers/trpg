using Microsoft.Extensions.Logging;
using TRPG.Application.Scenes.Results;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldSimulation.Publishing;

internal static class MovementSnapshotLog
{
    public static void Write(
        ILogger logger,
        Guid locationId,
        GameInstant gameTime,
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
                locationId,
                gameTime,
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
}
