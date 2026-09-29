using TRPG.Application.Abilities;
using TRPG.Application.Creatures.Results;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Mappers;

internal static class CreatureEffectsMapper
{
    public static CreatureEffects ToEffects(this Creature creature) =>
        new(
            creature.ActiveConditions.ToDictionary(
                condition => Enum.Parse<ConditionType>(condition.Key),
                condition => condition.Value
            ),
            creature.ActiveDots.Select(dot => dot.ToEffect()).ToArray(),
            creature.ActiveHots.Select(hot => hot.ToEffect()).ToArray(),
            creature.ActiveBuffs.Select(buff => buff.ToEffect()).ToArray()
        );
}
