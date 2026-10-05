using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class RoadClassifier
{
    internal const int StreetPortCount = 3;

    internal static IReadOnlyDictionary<RoadGraphPath, RoadClass> Classify(
        IReadOnlyList<RoadGraphNode> nodes,
        IReadOnlyList<RoadGraphPath> paths
    )
    {
        var classes = new Dictionary<RoadGraphPath, RoadClass>();

        if (paths.Count > 0)
        {
            Visit(RootJunction(nodes), null, paths, classes);
        }

        return classes;
    }

    private static RoadGraphNode RootJunction(IReadOnlyList<RoadGraphNode> nodes)
    {
        var ports = nodes.Where(node => node.Terminal is not null).ToArray();
        var centroid = new Point(
            ports.Average(port => port.Position.X),
            ports.Average(port => port.Position.Y)
        );

        return nodes
            .Where(node => node.Terminal is null)
            .OrderBy(node => RoadGeometry.Distance(node.Position, centroid))
            .First();
    }

    private static Downstream Visit(
        RoadGraphNode node,
        RoadGraphPath? arrivedBy,
        IReadOnlyList<RoadGraphPath> paths,
        Dictionary<RoadGraphPath, RoadClass> classes
    )
    {
        var total = node.Terminal is null
            ? new Downstream(0, 0)
            : new Downstream(1, node.Terminal.IsGate ? 1 : 0);

        foreach (var path in paths.Where(p => p != arrivedBy && (p.From == node || p.To == node)))
        {
            var next = path.From == node ? path.To : path.From;
            var below = Visit(next, path, paths, classes);
            classes[path] = ClassOf(below);
            total = new Downstream(total.Ports + below.Ports, total.Gates + below.Gates);
        }

        return total;
    }

    private static RoadClass ClassOf(Downstream below) =>
        below.Gates > 0 ? RoadClass.Avenue
        : below.Ports >= StreetPortCount ? RoadClass.Street
        : RoadClass.Lane;

    private record Downstream(int Ports, int Gates);
}
