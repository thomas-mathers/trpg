using TRPG.Application.Scenes.Queries;
using TRPG.GameSessions.Responses;

namespace TRPG.GameSessions.Mappers;

internal static class SceneDistrictExitDestinationMapper
{
    public static DistrictExitDestination ToSnapshot(
        this SceneDistrictExitDestination destination
    ) => new(destination.Name, destination.DistrictType.ToResponse());
}
