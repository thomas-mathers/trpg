using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Roads;

internal readonly record struct RoadCell(int Column, int Row);

internal sealed class RoadGrid
{
    private readonly bool[,] _blocked;

    internal RoadGrid(Footprint size, IReadOnlyCollection<RoadBuilding> buildings, double clearance)
    {
        Columns = Math.Max(1, (int)Math.Ceiling(size.Width));
        Rows = Math.Max(1, (int)Math.Ceiling(size.Depth));
        _blocked = new bool[Columns, Rows];

        for (var column = 0; column < Columns; column++)
        {
            for (var row = 0; row < Rows; row++)
            {
                var center = CenterOf(new RoadCell(column, row));
                _blocked[column, row] = buildings.Any(building =>
                    IsWithin(building, center, clearance)
                );
            }
        }
    }

    internal int Columns { get; }

    internal int Rows { get; }

    internal bool Contains(RoadCell cell) =>
        cell.Column >= 0 && cell.Column < Columns && cell.Row >= 0 && cell.Row < Rows;

    internal bool IsBlocked(RoadCell cell) => _blocked[cell.Column, cell.Row];

    internal RoadCell CellOf(Point point) =>
        new(
            Math.Clamp((int)Math.Floor(point.X), 0, Columns - 1),
            Math.Clamp((int)Math.Floor(point.Y), 0, Rows - 1)
        );

    internal Point CenterOf(RoadCell cell) => new(cell.Column + 0.5, cell.Row + 0.5);

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

    private static bool IsWithin(RoadBuilding building, Point point, double clearance)
    {
        var (sin, cos) = Math.SinCos(building.Placement.Angle);
        var dx = point.X - building.Placement.X;
        var dy = point.Y - building.Placement.Y;
        var local = new Point(dx * cos + dy * sin, -dx * sin + dy * cos);

        return Math.Abs(local.X) < building.Footprint.Width / 2 + clearance
            && Math.Abs(local.Y) < building.Footprint.Depth / 2 + clearance;
    }
}
