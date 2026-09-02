using TRPG.Application.GameTurns.Results;
using TRPG.GameSessions.Responses;

namespace TRPG.GameSessions.Mappers;

internal static class SceneBuildingExitDestinationMapper
{
    public static BuildingExitDestination ToSnapshot(
        this SceneBuildingExitDestination destination
    ) => new(destination.Name, destination.BuildingType.ToResponse());
}
