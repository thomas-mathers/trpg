using TRPG.Application.Scenes.Results;
using TRPG.GameSessions.Responses;

namespace TRPG.GameSessions.Mappers;

internal static class SceneConnectorLayoutMapper
{
    public static ConnectorLayoutWire ToWire(this SceneConnectorLayout connector) =>
        new(
            connector.ConnectorId,
            connector.DestinationLocationId,
            connector.ExitX,
            connector.ExitY
        );
}
