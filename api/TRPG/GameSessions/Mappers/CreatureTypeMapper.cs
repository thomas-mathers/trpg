using ContractCreatureType = TRPG.Contracts.Scenes.Responses.CreatureType;
using DataCreatureType = TRPG.Domain.Models.CreatureType;

namespace TRPG.GameSessions.Mappers;

internal static class CreatureTypeMapper
{
    public static ContractCreatureType ToContract(this DataCreatureType type) =>
        type switch
        {
            DataCreatureType.Human => ContractCreatureType.Human,
            DataCreatureType.Elf => ContractCreatureType.Elf,
            DataCreatureType.Dwarf => ContractCreatureType.Dwarf,
            DataCreatureType.Orc => ContractCreatureType.Orc,
            DataCreatureType.Halfling => ContractCreatureType.Halfling,
            DataCreatureType.Gnome => ContractCreatureType.Gnome,
            DataCreatureType.Undead => ContractCreatureType.Undead,
            DataCreatureType.Demon => ContractCreatureType.Demon,
            DataCreatureType.Beast => ContractCreatureType.Beast,
            DataCreatureType.Construct => ContractCreatureType.Construct,
            DataCreatureType.Elemental => ContractCreatureType.Elemental,
            DataCreatureType.Goblin => ContractCreatureType.Goblin,
            DataCreatureType.Wraith => ContractCreatureType.Wraith,
            DataCreatureType.Giant => ContractCreatureType.Giant,
            DataCreatureType.Dragon => ContractCreatureType.Dragon,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
}
