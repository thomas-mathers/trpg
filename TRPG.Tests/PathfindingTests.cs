using TRPG.Models;
using TRPG.Services;

namespace TRPG.Tests;

public class PathfindingTests {
    private static List<Edge> Graph(Dictionary<string, List<Edge>> graph, string node) {
        return graph.TryGetValue(node, out var edges) ? edges : [];
    }

    [Fact]
    public void Dijkstra_ReturnsMultiHopPath_InCorrectOrder() {
        // Arrange
        var graph = new Dictionary<string, List<Edge>> {
            ["A"] = [new Edge("B", 1), new Edge("C", 10)],
            ["B"] = [new Edge("C", 1)],
            ["C"] = []
        };

        // Act
        var result = Pathfinding.Dijkstra("A", "C", n => Graph(graph, n), e => e.Cost, e => e.To);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("B", result[0].To);
        Assert.Equal("C", result[1].To);
    }

    [Fact]
    public void Dijkstra_ReturnsShortestPath_WhenCheaperRouteRequiresMoreHops() {
        // Arrange — direct A→D costs 100; A→B→C→D costs 9
        var graph = new Dictionary<string, List<Edge>> {
            ["A"] = [new Edge("B", 3), new Edge("D", 100)],
            ["B"] = [new Edge("C", 3)],
            ["C"] = [new Edge("D", 3)],
            ["D"] = []
        };

        // Act
        var result = Pathfinding.Dijkstra("A", "D", n => Graph(graph, n), e => e.Cost, e => e.To);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("B", result[0].To);
        Assert.Equal("C", result[1].To);
        Assert.Equal("D", result[2].To);
    }

    [Fact]
    public void Dijkstra_ReturnsEmpty_WhenDestinationUnreachable() {
        // Arrange
        var graph = new Dictionary<string, List<Edge>> {
            ["A"] = [new Edge("B", 1)],
            ["B"] = [],
            ["C"] = []
        };

        // Act
        var result = Pathfinding.Dijkstra("A", "C", n => Graph(graph, n), e => e.Cost, e => e.To);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Dijkstra_NavigatesAroundObstacle_OnGrid() {
        // Arrange — 3×3 grid, (1,0) blocked; straight path is cut off
        //   0 1 2
        // 0 S X E
        // 1 . . .
        // 2 . . .
        var blocked = new HashSet<Point> { new(1, 0) };

        // Act
        var result = Pathfinding.Dijkstra(new Point(0, 0), new Point(2, 0), Neighbors, _ => 1f, p => p);

        // Assert — shortest detour is 4 steps: (0,1)→(1,1)→(2,1)→(2,0)
        Assert.Equal(4, result.Count);
        Assert.Equal(new Point(2, 0), result[^1]);
        return;

        IEnumerable<Point> Neighbors(Point p) {
            if (p.X > 0 && !blocked.Contains(new Point(p.X - 1, p.Y))) {
                yield return new Point(p.X - 1, p.Y);
            }

            if (p.X < 2 && !blocked.Contains(new Point(p.X + 1, p.Y))) {
                yield return new Point(p.X + 1, p.Y);
            }

            if (p.Y > 0 && !blocked.Contains(new Point(p.X, p.Y - 1))) {
                yield return new Point(p.X, p.Y - 1);
            }

            if (p.Y < 2 && !blocked.Contains(new Point(p.X, p.Y + 1))) {
                yield return new Point(p.X, p.Y + 1);
            }
        }
    }

    private sealed record Edge(string To, float Cost);
}