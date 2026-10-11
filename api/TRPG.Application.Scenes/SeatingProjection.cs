using TRPG.Application.Scenes.Events;
using TRPG.Application.Scenes.Results;

namespace TRPG.Application.Scenes;

public static class SeatingProjection
{
    public static SceneResult Apply(SceneResult scene, SeatingChangedEvent seating) =>
        scene with
        {
            Player = ApplyToCreature(scene.Player, seating),
            NearbyCreatures = scene
                .NearbyCreatures.Select(c => ApplyToCreature(c, seating))
                .ToArray(),
            NearbyProps = scene
                .NearbyProps.Select(p => ApplyToProp(p, scene.Player.Id, seating))
                .ToArray(),
        };

    private static SceneCreatureInfo ApplyToCreature(
        SceneCreatureInfo creature,
        SeatingChangedEvent seating
    ) =>
        creature.Id == seating.CreatureId
            ? creature with
            {
                Posture = seating.Posture,
                Placement = seating.Placement,
            }
            : creature;

    private static ScenePropInfo ApplyToProp(
        ScenePropInfo prop,
        Guid playerId,
        SeatingChangedEvent seating
    )
    {
        var isPlayer = seating.CreatureId == playerId;
        if (seating.SeatId is null)
        {
            return isPlayer && prop.IsOccupiedByPlayer
                ? prop with
                {
                    IsOccupied = false,
                    IsOccupiedByPlayer = false,
                }
                : prop;
        }

        return prop.Id == seating.SeatId
            ? prop with
            {
                IsOccupied = true,
                IsOccupiedByPlayer = isPlayer,
            }
            : prop;
    }
}
