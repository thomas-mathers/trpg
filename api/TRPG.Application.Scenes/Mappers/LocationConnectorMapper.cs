using TRPG.Application.Scenes.Results;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Mappers;

internal static class LocationConnectorMapper
{
    public static SceneConnectorLayout ToLayout(this LocationConnector connector) =>
        new(connector.Id, connector.DestinationLocationId, connector.ExitX, connector.ExitY);
}
