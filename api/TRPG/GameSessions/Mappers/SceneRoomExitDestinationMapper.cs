using TRPG.Application.GameTurns.Results;
using TRPG.GameSessions.Responses;

namespace TRPG.GameSessions.Mappers;

internal static class SceneRoomExitDestinationMapper
{
    public static RoomExitDestination ToSnapshot(this SceneRoomExitDestination destination) =>
        new(destination.Name, destination.BuildingType.ToResponse());
}
