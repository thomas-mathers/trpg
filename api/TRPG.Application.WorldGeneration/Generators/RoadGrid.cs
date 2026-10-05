using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal readonly record struct RoadCell(int Column, int Row);

internal sealed class RoadGrid
{
    private const double ObstacleClearance = 2.25;
    private const int EdgeMarginCells = 2;

    private readonly bool[,] _blocked;
    private readonly bool[,] _discouraged;

    internal RoadGrid(
        Footprint size,
        IReadOnlyCollection<RoadBuilding> buildings,
        IReadOnlyCollection<Point> entries
    )
    {
        Columns = Math.Max(1, CityGrid.CellOf(size.Width - 1e-9) + 1);
        Rows = Math.Max(1, CityGrid.CellOf(size.Depth - 1e-9) + 1);
        _blocked = new bool[Columns, Rows];
        _discouraged = new bool[Columns, Rows];
        var entryCells = entries.Select(CellOf).ToArray();

        for (var column = 0; column < Columns; column++)
        {
            for (var row = 0; row < Rows; row++)
            {
                var cell = new RoadCell(column, row);
                var center = CenterOf(cell);
                _blocked[column, row] = buildings.Any(building => IsNear(building, center));
                _discouraged[column, row] =
                    IsInEdgeMargin(cell) && !entryCells.Any(entry => IsInEntryStem(entry, cell));
            }
        }
    }

    internal int Columns { get; }

    internal int Rows { get; }

    internal bool Contains(RoadCell cell) =>
        cell.Column >= 0 && cell.Column < Columns && cell.Row >= 0 && cell.Row < Rows;

    internal bool IsBlocked(RoadCell cell) => _blocked[cell.Column, cell.Row];

    internal bool IsDiscouraged(RoadCell cell) => _discouraged[cell.Column, cell.Row];

    internal RoadCell CellOf(Point point) =>
        new(
            Math.Clamp(CityGrid.CellOf(point.X), 0, Columns - 1),
            Math.Clamp(CityGrid.CellOf(point.Y), 0, Rows - 1)
        );

    internal Point CenterOf(RoadCell cell) =>
        new(CityGrid.CentreOf(cell.Column), CityGrid.CentreOf(cell.Row));

    internal RoadCell NearestFree(RoadCell target)
    {
        var free = Enumerable
            .Range(0, Columns)
            .SelectMany(column =>
                Enumerable.Range(0, Rows).Select(row => new RoadCell(column, row))
            )
            .Where(cell => !IsBlocked(cell))
            .ToArray();

        return free.Length == 0
            ? target
            : free.OrderBy(cell =>
                    Math.Pow(cell.Column - target.Column, 2) + Math.Pow(cell.Row - target.Row, 2)
                )
                .ThenBy(cell => cell.Row)
                .ThenBy(cell => cell.Column)
                .First();
    }

    private bool IsInEdgeMargin(RoadCell cell) =>
        IsInBand(cell.Column, Columns, EdgeMarginCells)
        || IsInBand(cell.Row, Rows, EdgeMarginCells);

    private bool IsInEntryStem(RoadCell entry, RoadCell cell) =>
        (cell.Column == entry.Column && IsInSameEdgeBand(entry.Row, cell.Row, Rows))
        || (cell.Row == entry.Row && IsInSameEdgeBand(entry.Column, cell.Column, Columns));

    private static bool IsInBand(int index, int count, int width) =>
        index < width || index >= count - width;

    private static bool IsInSameEdgeBand(int first, int second, int count) =>
        (first < EdgeMarginCells && second < EdgeMarginCells)
        || (first >= count - EdgeMarginCells && second >= count - EdgeMarginCells);

    private static bool IsNear(RoadBuilding building, Point point)
    {
        var (sin, cos) = Math.SinCos(building.Placement.Angle);
        var dx = point.X - building.Placement.X;
        var dy = point.Y - building.Placement.Y;
        var local = new Point(dx * cos + dy * sin, -dx * sin + dy * cos);

        return Math.Abs(local.X) < building.Footprint.Width / 2 + ObstacleClearance - 1e-6
            && Math.Abs(local.Y) < building.Footprint.Depth / 2 + ObstacleClearance - 1e-6;
    }
}

internal sealed class RoadNetworkCells(RoadGrid grid)
{
    private readonly HashSet<RoadCell> _cells = [];

    internal bool Contains(RoadCell cell) => _cells.Contains(cell);

    internal void Add(RoadCell cell) => _cells.Add(cell);
}
