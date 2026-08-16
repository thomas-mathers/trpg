using TRPG.Application.Scenes.Queries;
using TRPG.Contracts;
using TRPG.Contracts.Scenes.Responses;

namespace TRPG.GameSessions.Mappers;

internal static class SceneNearbyBuildingInfoMapper
{
    public static NearbyBuildingSnapshot ToSnapshot(this SceneNearbyBuildingInfo building)
    {
        var type = building.Type.ToContract();
        return new NearbyBuildingSnapshot(building.Id, building.Name, type, type.ToDisplayName());
    }
}
