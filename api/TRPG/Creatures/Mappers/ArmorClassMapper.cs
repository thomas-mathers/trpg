using ContractArmorClass = TRPG.Inventory.Responses.ArmorClass;
using DataArmorClass = TRPG.Domain.Models.ArmorClass;

namespace TRPG.Creatures.Mappers;

internal static class ArmorClassMapper
{
    public static ContractArmorClass ToResponse(this DataArmorClass armorClass) =>
        armorClass switch
        {
            DataArmorClass.Cloth => ContractArmorClass.Cloth,
            DataArmorClass.Leather => ContractArmorClass.Leather,
            DataArmorClass.Mail => ContractArmorClass.Mail,
            DataArmorClass.Plate => ContractArmorClass.Plate,
            _ => throw new ArgumentOutOfRangeException(nameof(armorClass), armorClass, null),
        };
}
