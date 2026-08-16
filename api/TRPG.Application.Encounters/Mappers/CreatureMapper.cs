using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Mappers;

internal static class CreatureMapper
{
    public static HostileEncounterMember ToEncounterMember(this Creature creature) =>
        new(creature.Name, creature.CreatureType, creature.Level);
}
