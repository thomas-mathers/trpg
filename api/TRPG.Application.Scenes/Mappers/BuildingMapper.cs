using TRPG.Application.Scenes.Results;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Mappers;

internal static class BuildingMapper
{
    public static SceneBuildingLayout ToLayout(this Building building) =>
        new(
            building.Id,
            building.BuildingType,
            new Placement(building.X, building.Y, building.Angle),
            new Footprint(building.Width, building.Depth)
        );
}
