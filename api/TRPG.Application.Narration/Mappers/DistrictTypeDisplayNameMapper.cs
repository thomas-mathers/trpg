using TRPG.Contracts;
using ContractDistrictType = TRPG.Contracts.Scenes.Responses.DistrictType;
using DataDistrictType = TRPG.Domain.Models.DistrictType;

namespace TRPG.Application.Narration.Mappers;

internal static class DistrictTypeDisplayNameMapper
{
    public static string ToDisplayName(this DataDistrictType type) =>
        (
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
            }
        ).ToDisplayName();
}
