using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class LocationLayoutGenerator
{
    internal static LocationLayoutResult Generate(LocationLayoutInput input)
    {
        var context = new LocationLayoutContext(input);
        var exitByConnectorId = new Dictionary<Guid, ConnectorExit>();

        var roomFurniture = RoomLayoutPass.Run(context, exitByConnectorId);
        var exterior = ExteriorLayoutPass.Run(context, exitByConnectorId);
        var connectorNodes = ConnectorNodePlacer.Place(
            context,
            exitByConnectorId,
            exterior.PortNodeIdByConnectorId
        );
        var nodes = new List<TravelNode>([.. exterior.TravelNodes, .. connectorNodes]);
        var nodeById = nodes.ToDictionary(node => node.Id);

        return new LocationLayoutResult(
            [.. roomFurniture, .. exterior.Props],
            nodes,
            [
                .. exterior.PointConnectors,
                .. RoomPointConnectorGenerator.Generate(context, nodeById, roomFurniture),
                .. WildernessPointConnectorGenerator.Generate(context, nodeById),
            ]
        );
    }
}
