using TRPG.Application.Scenes.Results;
using TRPG.GameSessions.Responses;

namespace TRPG.GameSessions.Mappers;

internal static class SceneWildernessExitDestinationMapper
{
    public static WildernessExitDestination ToSnapshot(
        this SceneWildernessExitDestination destination
    ) => new(destination.Name);
}
