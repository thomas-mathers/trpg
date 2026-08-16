using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Mappers;

internal static class RaceMapper
{
    public static CreatureType ToCreatureType(this Race race) =>
        race switch
        {
            Race.Human => CreatureType.Human,
            Race.Elf => CreatureType.Elf,
            Race.Dwarf => CreatureType.Dwarf,
            Race.Orc => CreatureType.Orc,
            Race.Halfling => CreatureType.Halfling,
            Race.Gnome => CreatureType.Gnome,
            _ => throw new ArgumentOutOfRangeException(nameof(race), race, null),
        };
}
