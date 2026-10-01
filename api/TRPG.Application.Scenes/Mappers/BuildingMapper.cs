using TRPG.Application.Scenes.Results;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Mappers;

internal static class BuildingMapper
{
    public static SceneBoxLayout ToLayout(this Building building) =>
        new(
            building.Id,
            new Placement(building.X, building.Y, building.Angle),
            new Footprint(building.Width, building.Depth)
        );
}
