using ContractAttributeName = TRPG.Contracts.Combat.Responses.AttributeName;
using DataAttributeName = TRPG.Data.Models.AttributeName;

namespace TRPG.Application.Common.Mappers;

internal static class RequestEnumMappers
{
    public static DataAttributeName ToDomain(this ContractAttributeName attribute) =>
        attribute switch
        {
            ContractAttributeName.Strength => DataAttributeName.Strength,
            ContractAttributeName.Defense => DataAttributeName.Defense,
            ContractAttributeName.Dexterity => DataAttributeName.Dexterity,
            ContractAttributeName.Endurance => DataAttributeName.Endurance,
            ContractAttributeName.Stamina => DataAttributeName.Stamina,
            ContractAttributeName.Mana => DataAttributeName.Mana,
            ContractAttributeName.Intelligence => DataAttributeName.Intelligence,
            _
                => throw new ArgumentOutOfRangeException(
                    nameof(attribute),
                    attribute,
                    "Attribute is not allocatable."
                ),
        };
}
