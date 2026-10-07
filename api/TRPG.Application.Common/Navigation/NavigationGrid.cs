using TRPG.Application.Common.Algorithms;
using TRPG.Domain.Models;

namespace TRPG.Application.Common.Navigation;

public sealed class NavigationGrid
{
    private const int NoCell = -1;

    private readonly double _cellSize;
    private readonly int _columns;
    private readonly int _rows;
    private readonly bool[,] _blocked;

    public NavigationGrid(double width, double depth, double cellSize, Func<Point, bool> isBlocked)
    {
        _cellSize = cellSize;
        _columns = Math.Max(1, (int)Math.Ceiling(width / cellSize - 1e-9));
        _rows = Math.Max(1, (int)Math.Ceiling(depth / cellSize - 1e-9));
        _blocked = new bool[_columns, _rows];

        for (var column = 0; column < _columns; column++)
        {
            for (var row = 0; row < _rows; row++)
            {
                _blocked[column, row] = isBlocked(CentreOf(new Cell(column, row)));
            }
        }
    }

    public IReadOnlyList<Point> FindPath(Point from, Point to)
    {
        var start = NearestFree(from);
        var goal = NearestFree(to);

        if (start.Column == NoCell || goal.Column == NoCell)
        {
            return [from, to];
        }

        var cells = Graphs.ShortestPath(start, goal, FreeNeighbors, Distance);
        if (cells.Count == 0)
        {
            return [from, to];
        }

        return Straighten(from, to, cells, start, goal);
    }

    private IReadOnlyList<Point> Straighten(
        Point from,
        Point to,
        IReadOnlyList<Cell> cells,
        Cell start,
        Cell goal
    )
    {
        Point[] points = [from, .. cells.Skip(1).SkipLast(1).Select(CentreOf), to];
        var path = new List<Point> { from };
        var anchor = 0;

        while (anchor < points.Length - 1)
        {
            var farthest = anchor + 1;

            for (var candidate = points.Length - 1; candidate > anchor + 1; candidate--)
            {
                if (HasLineOfSight(points[anchor], points[candidate], start, goal))
                {
                    farthest = candidate;
                    break;
                }
            }

            path.Add(points[farthest]);
            anchor = farthest;
        }

        return path;
    }

    private bool HasLineOfSight(Point from, Point to, Cell start, Cell goal)
    {
        var length = Math.Sqrt(Math.Pow(to.X - from.X, 2) + Math.Pow(to.Y - from.Y, 2));
        var steps = Math.Max(1, (int)Math.Ceiling(length / (_cellSize / 4)));

        for (var step = 0; step <= steps; step++)
        {
            var fraction = (double)step / steps;
            var cell = CellOf(
                new Point(from.X + (to.X - from.X) * fraction, from.Y + (to.Y - from.Y) * fraction)
            );

            if (cell != start && cell != goal && _blocked[cell.Column, cell.Row])
            {
                return false;
            }
        }

        return true;
    }

    private Cell NearestFree(Point point)
    {
        var origin = CellOf(point);
        var farthestRing = Math.Max(_columns, _rows);

        for (var ring = 0; ring <= farthestRing; ring++)
        {
            var best = new Cell(NoCell, NoCell);
            var bestDistance = double.MaxValue;

            foreach (var cell in Ring(origin, ring).Where(IsFree))
            {
                var distance = DistanceTo(point, CentreOf(cell));
                if (distance < bestDistance)
                {
                    best = cell;
                    bestDistance = distance;
                }
            }

            if (best.Column != NoCell)
            {
                return best;
            }
        }

        return new Cell(NoCell, NoCell);
    }

    private IEnumerable<Cell> Ring(Cell origin, int radius)
    {
        for (var column = origin.Column - radius; column <= origin.Column + radius; column++)
        {
            for (var row = origin.Row - radius; row <= origin.Row + radius; row++)
            {
                var onRing =
                    Math.Max(Math.Abs(column - origin.Column), Math.Abs(row - origin.Row))
                    == radius;
                if (onRing && InBounds(column, row))
                {
                    yield return new Cell(column, row);
                }
            }
        }
    }

    private IEnumerable<Cell> FreeNeighbors(Cell cell)
    {
        for (var deltaColumn = -1; deltaColumn <= 1; deltaColumn++)
        {
            for (var deltaRow = -1; deltaRow <= 1; deltaRow++)
            {
                if (deltaColumn == 0 && deltaRow == 0)
                {
                    continue;
                }

                var neighbor = new Cell(cell.Column + deltaColumn, cell.Row + deltaRow);
                var cutsCorner =
                    deltaColumn != 0
                    && deltaRow != 0
                    && (
                        !IsFree(new Cell(cell.Column + deltaColumn, cell.Row))
                        || !IsFree(new Cell(cell.Column, cell.Row + deltaRow))
                    );

                if (IsFree(neighbor) && !cutsCorner)
                {
                    yield return neighbor;
                }
            }
        }
    }

    private bool IsFree(Cell cell) =>
        InBounds(cell.Column, cell.Row) && !_blocked[cell.Column, cell.Row];

    private bool InBounds(int column, int row) =>
        column >= 0 && column < _columns && row >= 0 && row < _rows;

    private Cell CellOf(Point point) =>
        new(
            Math.Clamp((int)Math.Floor(point.X / _cellSize), 0, _columns - 1),
            Math.Clamp((int)Math.Floor(point.Y / _cellSize), 0, _rows - 1)
        );

    private Point CentreOf(Cell cell) =>
        new((cell.Column + 0.5) * _cellSize, (cell.Row + 0.5) * _cellSize);

    private double Distance(Cell from, Cell to) => DistanceTo(CentreOf(from), CentreOf(to));

    private static double DistanceTo(Point from, Point to) =>
        Math.Sqrt(Math.Pow(to.X - from.X, 2) + Math.Pow(to.Y - from.Y, 2));

    private readonly record struct Cell(int Column, int Row);
}
