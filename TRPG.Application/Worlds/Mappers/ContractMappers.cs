using TRPG.Contracts;
using TRPG.Data.Models;
using Gender = TRPG.Data.Models.Gender;

namespace TRPG.Application.Worlds.Mappers;

internal static class ContractMappers
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

    public static Profession ToProfession(this PlayerClass playerClass) =>
        playerClass switch
        {
            PlayerClass.Knight => Profession.Knight,
            PlayerClass.Rogue => Profession.Rogue,
            PlayerClass.Ranger => Profession.Ranger,
            PlayerClass.Mage => Profession.Mage,
            PlayerClass.Cleric => Profession.Cleric,
            _ => throw new ArgumentOutOfRangeException(nameof(playerClass), playerClass, null),
        };

    public static Gender ToGender(this Contracts.Gender gender) =>
        gender switch
        {
            Contracts.Gender.Male => Gender.Male,
            Contracts.Gender.Female => Gender.Female,
            _ => throw new ArgumentOutOfRangeException(nameof(gender), gender, null),
        };
}
