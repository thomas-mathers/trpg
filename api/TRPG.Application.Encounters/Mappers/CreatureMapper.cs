using TRPG.Application.Combat;
using TRPG.Application.Encounters;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Mappers;

internal static class CreatureMapper
{
    public static EvadeParticipant ToEvadeParticipant(this Creature creature) =>
        new(
            creature.Dexterity,
            creature.CurrentHp,
            creature.MaximumHp,
            creature.CurrentAp,
            creature.MaximumAp
        );

    public static IntimidationParticipant ToIntimidationParticipant(this Creature creature) =>
        new(
            creature.Strength,
            creature.CurrentHp,
            creature.MaximumHp,
            creature.CurrentAp,
            creature.MaximumAp
        );
}
