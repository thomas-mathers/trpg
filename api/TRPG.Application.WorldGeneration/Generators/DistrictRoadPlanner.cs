using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal record RoadBuilding(Placement Placement, Footprint Footprint);

internal record RoadTerminal(Guid ConnectorId, bool IsGate, Point Start, Point Entry);

internal record PlannedRoad(RoadTerminal Terminal, IReadOnlyList<Point> Points);

internal static class DistrictRoadPlanner
{
    private const double Epsilon = 1e-9;

    internal static IReadOnlyList<PlannedRoad> Plan(
        Footprint size,
        IReadOnlyCollection<RoadBuilding> buildings,
        IReadOnlyCollection<RoadTerminal> terminals
    )
    {
        if (terminals.Count == 0)
        {
            return [];
        }

        var grid = new RoadGrid(
            size,
            buildings,
            terminals.Select(terminal => terminal.Entry).ToArray()
        );
        var hub = grid.NearestFree(grid.CellOf(HubTarget(terminals)));
        var network = new RoadNetworkCells(grid);
        var roads = new List<PlannedRoad>();
        network.Add(hub);

        foreach (var terminal in ByDistanceTo(grid.CenterOf(hub), terminals))
        {
            var route = RoadRouter.Route(
                grid,
                grid.CellOf(terminal.Entry),
                HeadingOf(terminal),
                network
            );

            foreach (var cell in route)
            {
                network.Add(cell);
            }

            roads.Add(new PlannedRoad(terminal, Polyline(grid, terminal, route)));
        }

        return TrimTrunkSpur(roads);
    }

    private static RoadCell HeadingOf(RoadTerminal terminal)
    {
        var dx = terminal.Entry.X - terminal.Start.X;
        var dy = terminal.Entry.Y - terminal.Start.Y;

        return Math.Abs(dx) > Math.Abs(dy)
            ? new RoadCell(Math.Sign(dx), 0)
            : new RoadCell(0, Math.Sign(dy));
    }

    private static IReadOnlyList<Point> Polyline(
        RoadGrid grid,
        RoadTerminal terminal,
        IReadOnlyList<RoadCell> route
    ) => RoadGeometry.Simplify([terminal.Start, .. route.Select(grid.CenterOf)]);

    private static IReadOnlyList<PlannedRoad> TrimTrunkSpur(List<PlannedRoad> roads)
    {
        if (roads.Count < 2)
        {
            return roads;
        }

        var trunk = roads[0].Points;
        var reach = roads.Skip(1).Max(road => ArcPosition(trunk, road.Points[^1]));
        roads[0] = roads[0] with { Points = CutAt(trunk, reach) };

        return roads;
    }

    private static double ArcPosition(IReadOnlyList<Point> points, Point target)
    {
        var travelled = 0.0;

        for (var index = 1; index < points.Count; index++)
        {
            var length = RoadGeometry.Distance(points[index - 1], points[index]);
            var toTarget = RoadGeometry.Distance(points[index - 1], target);
            var beyond = RoadGeometry.Distance(target, points[index]);

            if (Math.Abs(toTarget + beyond - length) < Epsilon)
            {
                return travelled + toTarget;
            }

            travelled += length;
        }

        return 0;
    }

    private static IReadOnlyList<Point> CutAt(IReadOnlyList<Point> points, double arc)
    {
        var kept = new List<Point> { points[0] };
        var travelled = 0.0;

        for (var index = 1; index < points.Count; index++)
        {
            var length = RoadGeometry.Distance(points[index - 1], points[index]);

            if (travelled + length >= arc - Epsilon)
            {
                var t = length == 0 ? 0 : (arc - travelled) / length;
                kept.Add(
                    new Point(
                        points[index - 1].X + (points[index].X - points[index - 1].X) * t,
                        points[index - 1].Y + (points[index].Y - points[index - 1].Y) * t
                    )
                );

                return RoadGeometry.Simplify(kept);
            }

            kept.Add(points[index]);
            travelled += length;
        }

        return points;
    }

    private static IEnumerable<RoadTerminal> ByDistanceTo(
        Point target,
        IReadOnlyCollection<RoadTerminal> terminals
    ) =>
        terminals
            .OrderBy(terminal => terminal.IsGate ? 0 : 1)
            .ThenBy(terminal => RoadGeometry.Distance(terminal.Entry, target))
            .ThenBy(terminal => terminal.Start.X)
            .ThenBy(terminal => terminal.Start.Y);

    private static Point HubTarget(IReadOnlyCollection<RoadTerminal> terminals)
    {
        var gates = terminals.Where(terminal => terminal.IsGate).ToArray();
        var northSouth = gates.Where(gate => gate.Start.X == gate.Entry.X).ToArray();
        var eastWest = gates.Where(gate => gate.Start.Y == gate.Entry.Y).ToArray();

        return new Point(
            AverageOrElse(northSouth, terminals, terminal => terminal.Entry.X),
            AverageOrElse(eastWest, terminals, terminal => terminal.Entry.Y)
        );
    }

    private static double AverageOrElse(
        IReadOnlyCollection<RoadTerminal> preferred,
        IReadOnlyCollection<RoadTerminal> fallback,
        Func<RoadTerminal, double> axis
    ) => (preferred.Count > 0 ? preferred : fallback).Average(axis);
}
