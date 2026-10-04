using TRPG.Application.Scenes.Results;
using TRPG.GameSessions.Responses;

namespace TRPG.GameSessions.Mappers;

internal static class SceneExitInfoMapper
{
    public static NearbyExitSnapshot ToSnapshot(this SceneExitInfo exit) =>
        new(
            exit.ConnectorId,
            exit.Description,
            exit.Destination.ToSnapshot(),
            exit.Direction?.ToResponse(),
            exit.IsVisited,
            exit.IsWayBack,
            exit.DestinationLocationId,
            exit.Placement.ToWire(),
            exit.Stairs?.ToResponse()
        );
}
