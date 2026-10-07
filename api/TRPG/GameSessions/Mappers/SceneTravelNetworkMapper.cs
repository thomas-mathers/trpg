using TRPG.Application.Scenes.Roads;
using TRPG.GameSessions.Responses;

namespace TRPG.GameSessions.Mappers;

internal static class SceneTravelNetworkMapper
{
    public static TravelNetworkSnapshot ToSnapshot(this SceneTravelNetwork network) =>
        new(
            Nodes: network
                .Nodes.Select(node => new TravelNodeSnapshot(
                    node.Id,
                    node.Position.ToWire(),
                    node.IsPort
                ))
                .ToArray(),
            Edges: network
                .Edges.Select(edge => new TravelEdgeSnapshot(
                    edge.Points.Select(point => point.ToWire()).ToArray(),
                    edge.Distance,
                    edge.Bidirectional
                ))
                .ToArray()
        );
}
