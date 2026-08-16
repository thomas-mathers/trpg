using TRPG.Application.Scenes.Queries;
using TRPG.Contracts.Scenes.Responses;

namespace TRPG.GameSessions.Mappers;

internal static class SceneExitDestinationMapper
{
    public static NearbyExitDestination ToSnapshot(this SceneExitDestination destination) =>
        destination switch
        {
            SceneDistrictExitDestination district => district.ToSnapshot(),
            SceneBuildingExitDestination building => building.ToSnapshot(),
            SceneRoomExitDestination room => room.ToSnapshot(),
            SceneWildernessExitDestination wilderness => wilderness.ToSnapshot(),
            _ => throw new ArgumentOutOfRangeException(nameof(destination)),
        };
}
