using TRPG.Application.Common.Algorithms;
using TRPG.Data.Models;

namespace TRPG.Tests.Application.Algorithms;

public class GraphsTests
{
    [Fact]
    public void ShortestPath_ReturnsMultiHopPath_InCorrectOrder()
    {
        // Arrange
        var graph = new Dictionary<string, Dictionary<string, float>>
        {
            ["A"] = new() { ["B"] = 1, ["C"] = 10 },
            ["B"] = new() { ["C"] = 1 },
            ["C"] = new(),
        };

        // Act
        var result = Graphs.ShortestPath(
            "A",
            "C",
            n => Neighbors(graph, n),
            (from, to) => graph[from][to]
        );

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("A", result[0]);
        Assert.Equal("B", result[1]);
        Assert.Equal("C", result[2]);
    }

    [Fact]
    public void ShortestPath_ReturnsShortestPath_WhenCheaperRouteRequiresMoreHops()
    {
        // Arrange — direct A→D costs 100; A→B→C→D costs 9
        var graph = new Dictionary<string, Dictionary<string, float>>
        {
            ["A"] = new() { ["B"] = 3, ["D"] = 100 },
            ["B"] = new() { ["C"] = 3 },
            ["C"] = new() { ["D"] = 3 },
            ["D"] = new(),
        };

        // Act
        var result = Graphs.ShortestPath(
            "A",
            "D",
            n => Neighbors(graph, n),
            (from, to) => graph[from][to]
        );

        // Assert
        Assert.Equal(4, result.Count);
        Assert.Equal("A", result[0]);
        Assert.Equal("B", result[1]);
        Assert.Equal("C", result[2]);
        Assert.Equal("D", result[3]);
    }

    [Fact]
    public void ShortestPath_ReturnsEmpty_WhenDestinationUnreachable()
    {
        // Arrange
        var graph = new Dictionary<string, Dictionary<string, float>>
        {
            ["A"] = new() { ["B"] = 1 },
            ["B"] = new(),
            ["C"] = new(),
        };

        // Act
        var result = Graphs.ShortestPath(
            "A",
            "C",
            n => Neighbors(graph, n),
            (from, to) => graph[from][to]
        );

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ShortestPath_NavigatesAroundObstacle_OnGrid()
    {
        // Arrange — 3×3 grid, (1,0) blocked; straight path is cut off
        //   0 1 2
        // 0 S X E
        // 1 . . .
        // 2 . . .
        var blocked = new HashSet<Point> { new((double)1, (double)0) };

        // Act
        var result = Graphs.ShortestPath(
            new Point(0, 0),
            new Point(2, 0),
            GridNeighbors,
            (_, _) => 1f
        );

        // Assert — shortest detour is 4 steps: (0,1)→(1,1)→(2,1)→(2,0)
        Assert.Equal(5, result.Count);
        Assert.Equal(new Point(0, 0), result[0]);
        Assert.Equal(new Point(2, 0), result[^1]);
        return;

        IEnumerable<Point> GridNeighbors(Point p)
        {
            if (p.X > 0 && !blocked.Contains(new Point(p.X - 1, p.Y)))
            {
                yield return new Point(p.X - 1, p.Y);
            }

            if (p.X < 2 && !blocked.Contains(new Point(p.X + 1, p.Y)))
            {
                yield return new Point(p.X + 1, p.Y);
            }

            if (p.Y > 0 && !blocked.Contains(new Point(p.X, p.Y - 1)))
            {
                yield return new Point(p.X, p.Y - 1);
            }

            if (p.Y < 2 && !blocked.Contains(new Point(p.X, p.Y + 1)))
            {
                yield return new Point(p.X, p.Y + 1);
            }
        }
    }

    [Fact]
    public void MinimumSpanningTree_ReturnsNMinusOneEdges_ForConnectedGraph()
    {
        // Arrange — 4 vertices, fully connected with varying costs
        var graph = new Dictionary<string, Dictionary<string, float>>
        {
            ["A"] = new() { ["B"] = 1, ["C"] = 5 },
            ["B"] = new()
            {
                ["A"] = 1,
                ["C"] = 2,
                ["D"] = 6,
            },
            ["C"] = new()
            {
                ["A"] = 5,
                ["B"] = 2,
                ["D"] = 3,
            },
            ["D"] = new() { ["C"] = 3, ["B"] = 6 },
        };

        // Act
        var result = Graphs.MinimumSpanningTree(
            "A",
            n => Neighbors(graph, n),
            (from, to) => graph[from][to]
        );

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void MinimumSpanningTree_SelectsCheapestEdges()
    {
        // Arrange — MST should pick A-B(1), B-C(2), C-D(3) and skip the more expensive alternatives
        var graph = new Dictionary<string, Dictionary<string, float>>
        {
            ["A"] = new() { ["B"] = 1, ["C"] = 5 },
            ["B"] = new()
            {
                ["A"] = 1,
                ["C"] = 2,
                ["D"] = 6,
            },
            ["C"] = new()
            {
                ["A"] = 5,
                ["B"] = 2,
                ["D"] = 3,
            },
            ["D"] = new() { ["C"] = 3, ["B"] = 6 },
        };

        // Act
        var result = Graphs.MinimumSpanningTree(
            "A",
            n => Neighbors(graph, n),
            (from, to) => graph[from][to]
        );

        // Assert
        Assert.Contains(("A", "B"), result);
        Assert.Contains(("B", "C"), result);
        Assert.Contains(("C", "D"), result);
    }

    [Fact]
    public void MinimumSpanningTree_ReturnsPartialTree_WhenGraphIsDisconnected()
    {
        // Arrange — A-B connected, C-D isolated from them
        var graph = new Dictionary<string, Dictionary<string, float>>
        {
            ["A"] = new() { ["B"] = 1 },
            ["B"] = new() { ["A"] = 1 },
            ["C"] = new() { ["D"] = 2 },
            ["D"] = new() { ["C"] = 2 },
        };

        // Act — starts from A, so C and D are unreachable
        var result = Graphs.MinimumSpanningTree(
            "A",
            n => Neighbors(graph, n),
            (from, to) => graph[from][to]
        );

        // Assert
        Assert.Single(result);
        Assert.Contains(("A", "B"), result);
    }

    private static IEnumerable<string> Neighbors(
        Dictionary<string, Dictionary<string, float>> graph,
        string node
    )
    {
        return graph.TryGetValue(node, out var neighbors) ? neighbors.Keys.AsEnumerable() : [];
    }
}
