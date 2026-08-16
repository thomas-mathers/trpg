using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Mappers;

internal static class PlayerClassMapper
{
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
}
