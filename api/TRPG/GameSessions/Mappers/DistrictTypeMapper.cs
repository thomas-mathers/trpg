using ContractDistrictType = TRPG.GameSessions.Responses.DistrictType;
using DataDistrictType = TRPG.Domain.Models.DistrictType;

namespace TRPG.GameSessions.Mappers;

internal static class DistrictTypeMapper
{
    public static ContractDistrictType ToResponse(this DataDistrictType type) =>
        type switch
        {
            DataDistrictType.Residential => ContractDistrictType.Residential,
            DataDistrictType.Scientific => ContractDistrictType.Scientific,
            DataDistrictType.CityCenter => ContractDistrictType.CityCenter,
            DataDistrictType.CityEntrance => ContractDistrictType.CityEntrance,
            DataDistrictType.Governmental => ContractDistrictType.Governmental,
            DataDistrictType.HolySite => ContractDistrictType.HolySite,
            DataDistrictType.Encampment => ContractDistrictType.Encampment,
        };
}
