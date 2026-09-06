using TRPG.Domain.Models;

namespace TRPG.Application.Narration.Mappers;

internal static class DistrictTypeDisplayNameMapper
{
    public static string ToDisplayName(this DistrictType type) =>
        type switch
        {
            DistrictType.Residential => "Residential",
            DistrictType.Scientific => "Scientific",
            DistrictType.CityCenter => "City Center",
            DistrictType.CityEntrance => "City Entrance",
            DistrictType.Governmental => "Governmental",
            DistrictType.HolySite => "Holy Site",
            DistrictType.Encampment => "Encampment",
        };
}
