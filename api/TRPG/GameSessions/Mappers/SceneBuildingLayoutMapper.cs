using TRPG.Application.Scenes.Results;
using TRPG.GameSessions.Responses;

namespace TRPG.GameSessions.Mappers;

internal static class SceneBuildingLayoutMapper
{
    public static BuildingLayoutWire ToWire(this SceneBuildingLayout building) =>
        new(
            building.Id,
            building.Type.ToResponse(),
            building.Placement.ToWire(),
            building.Footprint.ToWire()
        );
}
