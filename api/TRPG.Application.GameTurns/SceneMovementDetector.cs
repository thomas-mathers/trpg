using TRPG.Application.GameTurns.Events;
using TRPG.Application.GameTurns.Results;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal record SceneMovement(Guid CreatureId, string Name, CreatureMovementDirection Direction);

internal static class SceneMovementDetector
{
    public static IReadOnlyCollection<SceneMovement> Detect(
        SceneResult previous,
        SceneResult current
    )
    {
        if (previous.LocationId != current.LocationId)
        {
            return [];
        }

        var previousIds = previous.NearbyCreatures.Select(creature => creature.Id).ToHashSet();
        var currentIds = current.NearbyCreatures.Select(creature => creature.Id).ToHashSet();

        var arrived = current
            .NearbyCreatures.Where(creature =>
                !previousIds.Contains(creature.Id) && CanBeSeenMoving(creature)
            )
            .Select(creature => new SceneMovement(
                creature.Id,
                creature.Name,
                CreatureMovementDirection.Arrived
            ));
        var departed = previous
            .NearbyCreatures.Where(creature =>
                !currentIds.Contains(creature.Id) && CanBeSeenMoving(creature)
            )
            .Select(creature => new SceneMovement(
                creature.Id,
                creature.Name,
                CreatureMovementDirection.Departed
            ));

        return arrived.Concat(departed).ToArray();
    }

    private static bool CanBeSeenMoving(SceneCreatureInfo creature) =>
        creature.State is not (CreatureState.Dead or CreatureState.Sleeping)
        && !creature.IsRestrained;
}
