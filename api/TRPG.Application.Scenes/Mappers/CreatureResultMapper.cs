using TRPG.Application.Creatures.Results;
using TRPG.Application.Scenes.Results;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Mappers;

internal static class CreatureResultMapper
{
    public static SceneCreatureLayout ToLayout(this CreatureResult creature) =>
        new(creature.Id, new Placement(creature.X, creature.Y, creature.Angle));
}
