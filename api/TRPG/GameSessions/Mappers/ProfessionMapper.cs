using ContractProfession = TRPG.GameSessions.Responses.Profession;
using DataProfession = TRPG.Domain.Models.Profession;

namespace TRPG.GameSessions.Mappers;

internal static class ProfessionMapper
{
    public static ContractProfession ToResponse(this DataProfession profession) =>
        profession switch
        {
            DataProfession.Knight => ContractProfession.Knight,
            DataProfession.Rogue => ContractProfession.Rogue,
            DataProfession.Ranger => ContractProfession.Ranger,
            DataProfession.Mage => ContractProfession.Mage,
            DataProfession.Cleric => ContractProfession.Cleric,
            DataProfession.Mercenary => ContractProfession.Mercenary,
            DataProfession.Alchemist => ContractProfession.Alchemist,
            DataProfession.Blacksmith => ContractProfession.Blacksmith,
            DataProfession.Scholar => ContractProfession.Scholar,
            DataProfession.Merchant => ContractProfession.Merchant,
            DataProfession.Politician => ContractProfession.Politician,
            DataProfession.StableMaster => ContractProfession.StableMaster,
            DataProfession.Guard => ContractProfession.Guard,
            DataProfession.Baker => ContractProfession.Baker,
            DataProfession.Innkeeper => ContractProfession.Innkeeper,
            DataProfession.Tailor => ContractProfession.Tailor,
            DataProfession.Carpenter => ContractProfession.Carpenter,
            DataProfession.Jeweler => ContractProfession.Jeweler,
            DataProfession.Homemaker => ContractProfession.Homemaker,
            DataProfession.Bartender => ContractProfession.Bartender,
            DataProfession.Unemployed => ContractProfession.Unemployed,
            _ => throw new ArgumentOutOfRangeException(nameof(profession), profession, null),
        };
}
