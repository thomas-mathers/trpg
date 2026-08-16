using TRPG.Application.Scenes.Queries;
using TRPG.Contracts.Scenes.Responses;

namespace TRPG.GameSessions.Mappers;

internal static class SceneBuildingExitDestinationMapper
{
    public static BuildingExitDestination ToSnapshot(
        this SceneBuildingExitDestination destination
    ) => new(destination.Name, destination.BuildingType.ToContract());
}
