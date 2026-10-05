using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class RoadNetworkPlannerTests
{
    private readonly Location _district = Builders.MakeLocation(
        districtId: Guid.NewGuid(),
        width: 40,
        depth: 30
    );

    [Fact]
    public void Plan_ReturnsAnEmptyNetwork_WhenThereAreNoTerminals()
    {
        // Act
        var network = RoadNetworkPlanner.Plan(_district, [], []);

        // Assert
        Assert.Empty(network.Nodes);
        Assert.Empty(network.Edges);
    }

    [Fact]
    public void Plan_CreatesOnePortPerTerminalKeyedByItsConnector()
    {
        // Arrange
        var terminals = FourEdgeTerminals(gateEdges: 0);

        // Act
        var network = RoadNetworkPlanner.Plan(_district, [], terminals);

        // Assert
        var ports = network.Nodes.Where(node => node.Kind == RoadNodeKind.Port).ToArray();
        Assert.Equal(
            terminals.Select(terminal => terminal.ConnectorId).Order(),
            ports.Select(port => port.ConnectorId!.Value).Order()
        );
    }

    [Fact]
    public void Plan_FormsATree()
    {
        // Arrange
        var terminals = FourEdgeTerminals(gateEdges: 0);

        // Act
        var network = RoadNetworkPlanner.Plan(_district, [], terminals);

        // Assert
        Assert.Equal(network.Nodes.Count - 1, network.Edges.Count);
        Assert.All(
            network.Edges,
            edge =>
            {
                Assert.Contains(network.Nodes, node => node.Id == edge.FromNodeId);
                Assert.Contains(network.Nodes, node => node.Id == edge.ToNodeId);
            }
        );
    }

    [Fact]
    public void Plan_ScopesEveryNodeAndEdgeToTheDistrictAndItsWorld()
    {
        // Arrange
        var terminals = FourEdgeTerminals(gateEdges: 0);

        // Act
        var network = RoadNetworkPlanner.Plan(_district, [], terminals);

        // Assert
        Assert.All(
            network.Nodes,
            node =>
            {
                Assert.Equal(_district.Id, node.LocationId);
                Assert.Equal(_district.WorldId, node.WorldId);
            }
        );
        Assert.All(
            network.Edges,
            edge =>
            {
                Assert.Equal(_district.Id, edge.LocationId);
                Assert.Equal(_district.WorldId, edge.WorldId);
            }
        );
    }

    [Fact]
    public void Plan_KeepsEdgeEndpointsOutOfTheWaypoints()
    {
        // Arrange
        var terminals = FourEdgeTerminals(gateEdges: 0);

        // Act
        var network = RoadNetworkPlanner.Plan(_district, [], terminals);

        // Assert
        var positions = network.Nodes.ToDictionary(node => node.Id);
        Assert.All(
            network.Edges,
            edge =>
            {
                var from = positions[edge.FromNodeId];
                var to = positions[edge.ToNodeId];
                Assert.DoesNotContain(
                    edge.Waypoints.Points,
                    point =>
                        (point.X == from.X && point.Y == from.Y)
                        || (point.X == to.X && point.Y == to.Y)
                );
            }
        );
    }

    [Fact]
    public void Plan_MeasuresEachEdgeAlongItsWaypoints()
    {
        // Arrange
        var terminals = FourEdgeTerminals(gateEdges: 0);

        // Act
        var network = RoadNetworkPlanner.Plan(_district, [], terminals);

        // Assert
        var positions = network.Nodes.ToDictionary(node => node.Id);
        Assert.All(
            network.Edges,
            edge =>
            {
                var from = positions[edge.FromNodeId];
                var to = positions[edge.ToNodeId];
                var straight = Math.Sqrt(Math.Pow(to.X - from.X, 2) + Math.Pow(to.Y - from.Y, 2));
                Assert.True(edge.Length >= straight - 1e-6);
                Assert.True(edge.Length > 0);
            }
        );
    }

    [Fact]
    public void Plan_ClassifiesTheRoadsLeadingToGatesAsAvenues()
    {
        // Arrange
        var terminals = FourEdgeTerminals(gateEdges: 4);

        // Act
        var network = RoadNetworkPlanner.Plan(_district, [], terminals);

        // Assert
        Assert.All(network.Edges, edge => Assert.Equal(RoadClass.Avenue, edge.Class));
    }

    [Fact]
    public void Plan_ClassifiesRoadsThatOnlyServeFewPortsAsLanes()
    {
        // Arrange
        var terminals = FourEdgeTerminals(gateEdges: 0).Take(2).ToArray();

        // Act
        var network = RoadNetworkPlanner.Plan(_district, [], terminals);

        // Assert
        Assert.All(network.Edges, edge => Assert.Equal(RoadClass.Lane, edge.Class));
    }

    private static RoadTerminal[] FourEdgeTerminals(int gateEdges) =>
        [
            Terminal(startX: 20, startY: 0, angle: Math.PI, isGate: gateEdges > 0),
            Terminal(startX: 20, startY: 30, angle: 0, isGate: gateEdges > 1),
            Terminal(startX: 0, startY: 15, angle: Math.PI / 2, isGate: gateEdges > 2),
            Terminal(startX: 40, startY: 15, angle: 3 * Math.PI / 2, isGate: gateEdges > 3),
        ];

    private static RoadTerminal Terminal(double startX, double startY, double angle, bool isGate) =>
        new(
            Guid.NewGuid(),
            isGate,
            new Point(startX, startY),
            new Point(startX + 1.5 * Math.Sin(angle), startY - 1.5 * Math.Cos(angle))
        );
}
