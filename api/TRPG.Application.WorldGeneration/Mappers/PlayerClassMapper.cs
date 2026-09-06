using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Mappers;

public static class PlayerClassMapper
{
    public static Profession ToProfession(this PlayerClass playerClass) =>
        playerClass switch
        {
            PlayerClass.Knight => Profession.Knight,
            PlayerClass.Rogue => Profession.Rogue,
            PlayerClass.Ranger => Profession.Ranger,
            PlayerClass.Mage => Profession.Mage,
            PlayerClass.Cleric => Profession.Cleric,
        };
}
