using TRPG.Application.Scenes.Queries;
using TRPG.Extensions;
using TRPG.GameSessions.Responses;

namespace TRPG.GameSessions.Mappers;

internal static class SceneNearbyBuildingInfoMapper
{
    public static NearbyBuildingSnapshot ToSnapshot(this SceneNearbyBuildingInfo building)
    {
        var type = building.Type.ToResponse();
        return new NearbyBuildingSnapshot(building.Id, building.Name, type, type.ToDisplayName());
    }
}
