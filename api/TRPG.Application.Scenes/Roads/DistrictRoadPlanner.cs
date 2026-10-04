using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Roads;

public record RoadBuilding(Placement Placement, Footprint Footprint);

public record RoadTerminal(Point Start, Point Entry);

public record DistrictRoad(IReadOnlyList<Point> Points, double Width);

public static class DistrictRoadPlanner
{
    public const double RoadWidth = 2.5;

    private const double Clearance = RoadWidth / 2;
    private const double Epsilon = 1e-9;

    public static IReadOnlyList<DistrictRoad> Plan(
        Footprint size,
        IReadOnlyCollection<RoadBuilding> buildings,
        IReadOnlyCollection<RoadTerminal> terminals
    )
    {
        if (terminals.Count == 0)
        {
            return [];
        }

        var grid = new RoadGrid(size, buildings, Clearance);
        var hub = grid.NearestFree(grid.CellOf(Centroid(terminals)));
        var network = new HashSet<RoadCell> { hub };
        var roads = new List<DistrictRoad>();

        foreach (var terminal in ByDistanceTo(grid.CenterOf(hub), terminals))
        {
            var cells = RoadRouter.Route(grid, grid.CellOf(terminal.Entry), network);
            network.UnionWith(cells);
            roads.Add(new DistrictRoad(Polyline(grid, terminal, cells), RoadWidth));
        }

        return roads;
    }

    private static IEnumerable<RoadTerminal> ByDistanceTo(
        Point target,
        IReadOnlyCollection<RoadTerminal> terminals
    ) =>
        terminals
            .OrderBy(terminal => Distance(terminal.Entry, target))
            .ThenBy(terminal => terminal.Start.X)
            .ThenBy(terminal => terminal.Start.Y);

    private static Point Centroid(IReadOnlyCollection<RoadTerminal> terminals) =>
        new(
            terminals.Average(terminal => terminal.Entry.X),
            terminals.Average(terminal => terminal.Entry.Y)
        );

    private static double Distance(Point a, Point b) =>
        Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    private static IReadOnlyList<Point> Polyline(
        RoadGrid grid,
        RoadTerminal terminal,
        IReadOnlyList<RoadCell> cells
    )
    {
        var centers = cells.Select(grid.CenterOf).ToList();
        AlignFirstRunTo(terminal.Entry, centers);

        return Simplify([terminal.Start, terminal.Entry, .. centers]);
    }

    private static void AlignFirstRunTo(Point entry, List<Point> centers)
    {
        var verticalRun = RunLength(centers, point => point.X);
        var vertical = verticalRun > 1;
        var run = vertical ? verticalRun : RunLength(centers, point => point.Y);

        for (var index = 0; index < run; index++)
        {
            centers[index] = vertical
                ? centers[index] with
                {
                    X = entry.X,
                }
                : centers[index] with
                {
                    Y = entry.Y,
                };
        }
    }

    private static int RunLength(IReadOnlyList<Point> points, Func<Point, double> axis)
    {
        var run = 1;

        while (run < points.Count && Math.Abs(axis(points[run]) - axis(points[0])) < Epsilon)
        {
            run++;
        }

        return run;
    }

    private static IReadOnlyList<Point> Simplify(IReadOnlyList<Point> points)
    {
        var simplified = new List<Point>();

        foreach (var point in points)
        {
            if (simplified.Count > 0 && Distance(simplified[^1], point) < Epsilon)
            {
                continue;
            }

            while (simplified.Count >= 2 && IsCollinear(simplified[^2], simplified[^1], point))
            {
                simplified.RemoveAt(simplified.Count - 1);
            }

            simplified.Add(point);
        }

        return simplified;
    }

    private static bool IsCollinear(Point a, Point b, Point c) =>
        Math.Abs((b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X)) < Epsilon;
}
