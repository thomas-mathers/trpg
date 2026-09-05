using TRPG.Application.Combat;
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
}
