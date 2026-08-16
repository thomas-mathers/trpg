using ContractDistrictType = TRPG.Contracts.Scenes.Responses.DistrictType;
using DataDistrictType = TRPG.Data.Models.DistrictType;

namespace TRPG.GameSessions.Mappers;

internal static class DistrictTypeMapper
{
    public static ContractDistrictType ToContract(this DataDistrictType type) =>
        type switch
        {
            DataDistrictType.Residential => ContractDistrictType.Residential,
            DataDistrictType.Scientific => ContractDistrictType.Scientific,
            DataDistrictType.CityCenter => ContractDistrictType.CityCenter,
            DataDistrictType.CityEntrance => ContractDistrictType.CityEntrance,
            DataDistrictType.Governmental => ContractDistrictType.Governmental,
            DataDistrictType.HolySite => ContractDistrictType.HolySite,
            DataDistrictType.Encampment => ContractDistrictType.Encampment,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
}
